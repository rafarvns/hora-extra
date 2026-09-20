using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using HoraExtra.Network;
using HoraExtra.Network.Models;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Ponte entre o sistema de tarefas e o servidor UDP.
    ///
    /// Responsabilidades:
    ///   - Registrar o catálogo de tarefas após CONN_SUCCESS (task_catalog_register).
    ///   - Enviar solicitação de atribuição aleatória de tarefas (task_assign_request, payload vazio).
    ///   - Receber broadcast do servidor com as tasks atribuídas (task_assigned).
    ///   - Enviar task_start_interaction ao servidor para iniciar minigame (pending → in_progress).
    ///   - Enviar task_complete_attempt ao servidor com resultado do minigame QTE.
    ///   - Receber broadcast task_updated e manter estado local das tasks do jogador local.
    ///
    /// Uso: adicione este MonoBehaviour a um GameObject persistente na cena.
    /// O catálogo inicial pode ser configurado via Inspector ou via código antes do Start.
    /// </summary>
    public class TaskSystemBridge : MonoBehaviour
    {
        // === Singleton ===
        public static TaskSystemBridge Instance { get; private set; }

        /// <summary>
        /// Garante que existe uma instância do TaskSystemBridge. Se não existir na cena,
        /// cria um GameObject em runtime com DontDestroyOnLoad.
        ///
        /// Mesmo padrão de <see cref="SocketManager.EnsureExists"/> e
        /// <see cref="RemotePlayerSpawner.EnsureExists"/>: permite que cenas de level
        /// (ex: SCN_FirstFloor) funcionem sem precisar do GameObject pré-configurado no
        /// Inspector. O catálogo inicial é preenchido programaticamente no Awake
        /// (EnsureCoffeeMakerEntry / EnsurePaperCollectEntry), então a instância criada
        /// em runtime registra o mesmo catálogo da versão configurada à mão.
        ///
        /// O SocketManager é garantido ANTES do AddComponent porque o OnEnable deste
        /// componente assina CONN_SUCCESS / TASK_ASSIGNED / TASK_UPDATED via
        /// SocketManager.Instance?.On(...) — com Instance nulo os `?.` viram no-op e a
        /// ponte nunca receberia os eventos.
        /// </summary>
        public static TaskSystemBridge EnsureExists()
        {
            if (Instance == null)
            {
                SocketManager.EnsureExists();

                Debug.Log("[GAMEPLAY] TaskSystemBridge não encontrado na cena — criando runtime.");
                var go = new GameObject("TaskSystemBridge (auto-created)");
                go.AddComponent<TaskSystemBridge>();
                // Awake roda imediatamente no AddComponent; Instance já está setado aqui.
            }
            return Instance;
        }

        // === Inspector ===
        [Header("Task Catalog — entradas iniciais da cena")]
        [Tooltip("Lista de tarefas que serão registradas no servidor ao conectar.")]
        [SerializeField] private List<TaskEntryData> _initialCatalog = new List<TaskEntryData>();

        [Header("Auto-assign")]
        [Tooltip("Se marcado, solicita automaticamente a atribuição de tasks (task_assign_request) " +
                 "logo após o catálogo ser registrado no CONN_SUCCESS. Desmarque para disparar manualmente via RequestMyTasks().")]
        [SerializeField] private bool _autoRequestTasksOnConnect = true;

        [Tooltip("Atraso (s) entre registrar o catálogo e pedir as tasks. Garante que o servidor " +
                 "processe task_catalog_register antes de task_assign_request (UDP não garante ordem).")]
        [SerializeField] private float _autoRequestDelaySeconds = 0.5f;

        // === Estado privado ===
        private bool _catalogRegistered = false;

        /// <summary>
        /// Tasks atribuídas ao jogador local, keyed por taskId.
        /// Populado via task_assigned e atualizado via task_updated.
        /// </summary>
        private Dictionary<string, AssignedTask> _myTasks = new Dictionary<string, AssignedTask>();

        // === Eventos públicos (Observer pattern) ===
        /// <summary>
        /// Disparado quando o servidor faz broadcast de tasks atribuídas a um jogador.
        /// Parâmetros: (playerId, tasks)
        /// </summary>
        public static event Action<string, List<AssignedTask>> OnTaskAssigned;

        /// <summary>
        /// Disparado quando o servidor faz broadcast de atualização de status de uma task.
        /// Parâmetros: (playerId, updatedTask)
        /// O updatedTask reflete o estado autoritativo do servidor.
        /// </summary>
        public static event Action<string, AssignedTask> OnTaskUpdated;

        /// <summary>
        /// Disparado quando o servidor RECUSA um task_progress por regra de gameplay
        /// (item desconhecido, destino errado, item já contabilizado...).
        ///
        /// É unicast: só chega para quem enviou o pacote recusado. Quem escuta deve
        /// desfazer o efeito otimista — na prática, devolver o item para a mão do
        /// jogador — e exibir Message.
        /// </summary>
        public static event Action<TaskRejectedPayload> OnTaskRejected;

        // === Lifecycle ===

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Catálogo inicial: garantir entradas padrão caso não configuradas no Inspector.
                EnsureCoffeeMakerEntry();
                EnsurePaperCollectEntry();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // Assina CONN_SUCCESS para registrar catálogo logo após conexão estabelecida.
            SocketManager.Instance?.On(NetworkEvents.CONNECTION_SUCCESS, OnConnectionSuccess);

            // Assina broadcast de atribuição de tasks.
            SocketManager.Instance?.On(NetworkEvents.TASK_ASSIGNED, OnTaskAssignedReceived);

            // Assina broadcast de atualização de status de task.
            SocketManager.Instance?.On(NetworkEvents.TASK_UPDATED, OnTaskUpdatedReceived);

            // Assina recusa de gameplay (unicast ao remetente).
            SocketManager.Instance?.On(NetworkEvents.TASK_REJECTED, OnTaskRejectedReceived);

            // Caso a conexão UDP já tenha sido estabelecida ANTES desta cena carregar
            // (ex: modo guest conecta na cena de menu), o CONN_SUCCESS já disparou e
            // não vai disparar de novo. Inicializa imediatamente para não perder o evento.
            if (SocketManager.Instance != null && SocketManager.Instance.IsConnected && !_catalogRegistered)
            {
                Debug.Log("[GAMEPLAY] TaskSystemBridge — conexão já estabelecida no OnEnable, inicializando catálogo agora.");
                HandleConnectionEstablished();
            }
        }

        private void OnDisable()
        {
            SocketManager.Instance?.Off(NetworkEvents.CONNECTION_SUCCESS, OnConnectionSuccess);
            SocketManager.Instance?.Off(NetworkEvents.TASK_ASSIGNED, OnTaskAssignedReceived);
            SocketManager.Instance?.Off(NetworkEvents.TASK_UPDATED, OnTaskUpdatedReceived);
            SocketManager.Instance?.Off(NetworkEvents.TASK_REJECTED, OnTaskRejectedReceived);
        }

        // === Métodos públicos ===

        /// <summary>
        /// Solicita ao servidor que sorteie e atribua N tasks aleatórias ao jogador local.
        /// Payload é vazio ({}). O playerId é obtido da sessão no servidor.
        /// Deve ser chamado após a conexão estar estabelecida e o catálogo registrado.
        /// </summary>
        public void RequestMyTasks()
        {
            SocketManager.Instance.Emit(NetworkEvents.TASK_ASSIGN_REQUEST, new { });
            Debug.Log("[NETWORK] task_assign_request enviado.");
        }

        /// <summary>
        /// Inicia interação com um objeto de task. Transição: pending → in_progress.
        /// Shape enviado: { taskId: string }
        /// </summary>
        public void SendStartInteraction(string taskId)
        {
            var payload = new TaskStartInteractionPayload { TaskId = taskId };
            SocketManager.Instance.Emit(NetworkEvents.TASK_START_INTERACTION, payload);
            Debug.Log($"[NETWORK] task_start_interaction enviado — taskId={taskId}");
        }

        /// <summary>
        /// Reporta o resultado do minigame QTE ao servidor.
        /// Shape enviado: { taskId: string, success: boolean }
        /// O servidor determina o status final (completed ou failed) autoritativamente.
        /// </summary>
        public void SendCompleteAttempt(string taskId, bool success)
        {
            var payload = new TaskCompleteAttemptPayload { TaskId = taskId, Success = success };
            SocketManager.Instance.Emit(NetworkEvents.TASK_COMPLETE_ATTEMPT, payload);
            Debug.Log($"[NETWORK] task_complete_attempt enviado — taskId={taskId} success={success}");
        }

        /// <summary>
        /// Reporta a coleta de +1 item de uma task incremental (ex: 'collect') ao servidor.
        /// Shape enviado: { taskId: string }
        /// O servidor soma +1 autoritativamente e decide as transições
        /// pending → in_progress → completed (ver TaskService.incrementProgress no backend).
        /// </summary>
        public void SendProgress(string taskId)
        {
            SendProgress(taskId, null, null);
        }

        /// <summary>
        /// Reporta a entrega de +1 item de uma task incremental, identificando QUAL item
        /// foi entregue e ONDE.
        /// Shape enviado: { taskId: string, itemId?: string, slotId?: string }
        ///
        /// Com itemId, o servidor valida que o item pertence à task, que o destino confere
        /// (quando a entrada declara pairs) e que ele ainda não foi contabilizado. Em caso
        /// de recusa, responde task_rejected em vez de task_updated — ver OnTaskRejected.
        ///
        /// itemId/slotId nulos são omitidos do JSON (NullValueHandling.Ignore no DTO), então
        /// este overload é o mesmo pacote de antes quando chamado sem eles.
        /// </summary>
        public void SendProgress(string taskId, string itemId, string slotId)
        {
            var payload = new TaskProgressPayload
            {
                TaskId = taskId,
                ItemId = string.IsNullOrEmpty(itemId) ? null : itemId,
                SlotId = string.IsNullOrEmpty(slotId) ? null : slotId,
            };
            SocketManager.Instance.Emit(NetworkEvents.TASK_PROGRESS, payload);
            Debug.Log($"[NETWORK] task_progress enviado — taskId={taskId} itemId={itemId ?? "-"} slotId={slotId ?? "-"}");
        }

        /// <summary>
        /// Retorna a lista de tasks atribuídas ao jogador local.
        /// Pode ser usada por CoffeeMakerInteraction para gate de proximidade.
        /// </summary>
        public List<AssignedTask> GetMyTasks()
        {
            return new List<AssignedTask>(_myTasks.Values);
        }

        /// <summary>
        /// Retorna a primeira task do jogador local que bate com o tipo e status informados.
        /// Retorna null se não encontrar nenhuma.
        /// </summary>
        public AssignedTask FindMyTask(string type, params string[] statuses)
        {
            var statusSet = new HashSet<string>(statuses);
            foreach (var task in _myTasks.Values)
            {
                if (task.Type == type && statusSet.Contains(task.Status))
                    return task;
            }
            return null;
        }

        // === Handlers de rede ===

        /// <summary>
        /// Callback de CONN_SUCCESS: registra o catálogo de tarefas na sala.
        /// Chamado na main thread (SocketManager drena _mainThreadQueue no Update).
        /// </summary>
        private void OnConnectionSuccess(JToken data)
        {
            HandleConnectionEstablished();
        }

        /// <summary>
        /// Registra o catálogo e (opcionalmente) solicita a atribuição de tasks.
        /// Chamado tanto pelo evento CONN_SUCCESS quanto diretamente no OnEnable
        /// quando a conexão já estava estabelecida antes desta cena carregar.
        /// </summary>
        private void HandleConnectionEstablished()
        {
            _catalogRegistered = false;
            _myTasks.Clear();
            RegisterCatalog();

            // Solicita a atribuição automática de tasks após o catálogo ser registrado.
            // O delay garante que o servidor processe task_catalog_register antes do
            // task_assign_request — assignRandomTasks lança erro se o catálogo da sala
            // ainda não existir (ver TaskService.assignRandomTasks no backend).
            if (_autoRequestTasksOnConnect)
                StartCoroutine(RequestTasksAfterCatalog());
        }

        /// <summary>
        /// Aguarda um curto intervalo após o registro do catálogo e então solicita
        /// a atribuição aleatória de tasks ao servidor.
        /// </summary>
        private IEnumerator RequestTasksAfterCatalog()
        {
            yield return new WaitForSeconds(_autoRequestDelaySeconds);
            RequestMyTasks();
        }

        /// <summary>
        /// Callback de task_assigned: loga cada task recebida, atualiza estado local
        /// para o jogador local e dispara o evento Observer.
        /// Chamado na main thread.
        /// </summary>
        private void OnTaskAssignedReceived(JToken data)
        {
            try
            {
                var payload = data.ToObject<TaskAssignedPayload>();
                if (payload == null)
                {
                    Debug.LogWarning("[NETWORK] task_assigned — payload nulo após deserialização.");
                    return;
                }

                if (payload.Tasks == null || payload.Tasks.Count == 0)
                {
                    Debug.LogWarning($"[NETWORK] task_assigned para playerId={payload.PlayerId} — lista de tasks vazia.");
                    return;
                }

                Debug.Log($"[GAMEPLAY] tasks recebidas para playerId={payload.PlayerId} — {payload.Tasks.Count} task(s).");
                foreach (var task in payload.Tasks)
                {
                    Debug.Log($"[GAMEPLAY]   task id={task.Id} type={task.Type} targetCount={task.TargetCount} " +
                              $"currentProgress={task.CurrentProgress} status={task.Status}");
                }

                // Armazena tasks do jogador local. MESCLA em vez de substituir: com a fila
                // sequencial, o servidor reenvia task_assigned a cada nova tarefa liberada,
                // e limpar aqui apagaria as já concluídas do HUD.
                // A limpeza acontece uma vez só, no HandleConnectionEstablished.
                if (IsLocalPlayer(payload.PlayerId))
                {
                    foreach (var task in payload.Tasks)
                        _myTasks[task.Id] = task;
                }

                OnTaskAssigned?.Invoke(payload.PlayerId, payload.Tasks);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NETWORK] Falha ao parsear task_assigned: {ex.Message} | raw: {data}");
            }
        }

        /// <summary>
        /// Callback de task_updated: atualiza a task local na lista do jogador e
        /// dispara evento público OnTaskUpdated(playerId, task).
        /// Chamado na main thread.
        /// </summary>
        private void OnTaskUpdatedReceived(JToken data)
        {
            try
            {
                var payload = data.ToObject<TaskUpdatedPayload>();
                if (payload == null)
                {
                    Debug.LogWarning("[NETWORK] task_updated — payload nulo após deserialização.");
                    return;
                }

                Debug.Log($"[GAMEPLAY] task_updated recebido — taskId={payload.TaskId} " +
                          $"status={payload.Status} progress={payload.CurrentProgress} " +
                          $"playerId={payload.PlayerId}");

                // Atualiza estado local se for task do jogador local.
                if (IsLocalPlayer(payload.PlayerId) && _myTasks.TryGetValue(payload.TaskId, out var existingTask))
                {
                    existingTask.Status = payload.Status;
                    existingTask.CurrentProgress = payload.CurrentProgress;
                }

                // Constrói objeto AssignedTask para o evento (snapshot do estado atual ou parcial).
                AssignedTask updatedTask;
                if (IsLocalPlayer(payload.PlayerId) && _myTasks.TryGetValue(payload.TaskId, out var localTask))
                {
                    updatedTask = localTask;
                }
                else
                {
                    // Para tasks de outros jogadores (broadcast), cria objeto mínimo com os dados disponíveis.
                    updatedTask = new AssignedTask
                    {
                        Id              = payload.TaskId,
                        Status          = payload.Status,
                        CurrentProgress = payload.CurrentProgress
                    };
                }

                OnTaskUpdated?.Invoke(payload.PlayerId, updatedTask);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NETWORK] Falha ao parsear task_updated: {ex.Message} | raw: {data}");
            }
        }

        /// <summary>
        /// Callback de task_rejected: o servidor recusou um task_progress deste cliente.
        /// Repassa o motivo para quem enviou (tipicamente o TaskDepositPoint), que desfaz
        /// o efeito otimista devolvendo o item à mão.
        /// Chamado na main thread.
        /// </summary>
        private void OnTaskRejectedReceived(JToken data)
        {
            try
            {
                var payload = data.ToObject<TaskRejectedPayload>();
                if (payload == null)
                {
                    Debug.LogWarning("[NETWORK] task_rejected — payload nulo após deserialização.");
                    return;
                }

                Debug.Log($"[NETWORK] task_rejected — code={payload.Code} taskId={payload.TaskId} " +
                          $"itemId={payload.ItemId ?? "-"} message=\"{payload.Message}\"");

                OnTaskRejected?.Invoke(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NETWORK] Falha ao parsear task_rejected: {ex.Message} | raw: {data}");
            }
        }

        // === Métodos privados ===

        /// <summary>
        /// Verifica se o playerId corresponde ao jogador local.
        /// Usa SocketManager.LocalPlayerId como referência.
        /// </summary>
        private bool IsLocalPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            var localId = SocketManager.Instance?.LocalPlayerId;
            return localId == playerId;
        }

        /// <summary>
        /// Tipo de task das coletas de papéis/documentos. Usado por MissionPaperCollectible
        /// para localizar a task certa via FindMyTask.
        /// </summary>
        public const string PAPER_COLLECT_TYPE = "collect";

        private const string PAPER_COLLECT_TASK_ID = "task-collect-papers-01";

        /// <summary>
        /// Garante que o catálogo inicial contém a entrada de coleta de documentos.
        /// targetCount = 4 espelha a quantidade de papéis na cena (objetos com
        /// MissionPaperCollectible). Ajuste aqui se mudar o número de coletáveis.
        /// </summary>
        private void EnsurePaperCollectEntry()
        {
            const int PAPER_COUNT = 4;
            foreach (var entry in _initialCatalog)
            {
                if (entry.id == PAPER_COLLECT_TASK_ID) return;
            }
            _initialCatalog.Add(new TaskEntryData
            {
                id          = PAPER_COLLECT_TASK_ID,
                description = "Colete os documentos",
                type        = PAPER_COLLECT_TYPE,
                targetCount = PAPER_COUNT
            });
            Debug.Log("[GAMEPLAY] TaskSystemBridge — entrada collect (documentos) adicionada ao catálogo inicial.");
        }

        /// <summary>
        /// Garante que o catálogo inicial contém a entrada da cafeteira.
        /// Adicionada programaticamente caso o Inspector não tenha sido configurado.
        /// </summary>
        private void EnsureCoffeeMakerEntry()
        {
            const string COFFEE_TASK_ID = "task-coffee-maker-01";
            bool found = false;
            foreach (var entry in _initialCatalog)
            {
                if (entry.id == COFFEE_TASK_ID) { found = true; break; }
            }
            if (!found)
            {
                _initialCatalog.Add(new TaskEntryData
                {
                    id          = COFFEE_TASK_ID,
                    description = "Prepare o café",
                    type        = "coffee_maker",
                    targetCount = 3
                });
                Debug.Log("[GAMEPLAY] TaskSystemBridge — entrada coffee_maker adicionada ao catálogo inicial.");
            }
        }

        /// <summary>
        /// Constrói e envia o pacote task_catalog_register com as entradas do catálogo inicial.
        /// Shape enviado: { tasks: [{ id, description, type, targetCount }] } — sem npcId.
        /// </summary>
        private void RegisterCatalog()
        {
            if (_catalogRegistered)
            {
                Debug.Log("[GAMEPLAY] TaskSystemBridge.RegisterCatalog — catálogo já registrado, ignorando.");
                return;
            }

            var tasks = new List<TaskEntry>();
            foreach (var entry in _initialCatalog)
            {
                tasks.Add(new TaskEntry
                {
                    Id          = entry.id,
                    Description = entry.description,
                    Type        = entry.type,
                    TargetCount = entry.targetCount,
                    // Listas vazias viram null para o NullValueHandling.Ignore omitir o campo:
                    // um "items": [] na rede significaria "nenhum item é válido" e travaria a task.
                    Items       = ToNullIfEmpty(entry.items),
                    Pairs       = ToPairList(entry.pairs),
                });
            }

            var payload = new TaskCatalogRegisterPayload { Tasks = tasks };
            SocketManager.Instance.Emit(NetworkEvents.TASK_CATALOG_REGISTER, payload);
            _catalogRegistered = true;

            Debug.Log($"[NETWORK] task_catalog_register enviado — {tasks.Count} tarefa(s).");
        }

        /// <summary>Devolve null para lista nula ou vazia, para o campo ser omitido do JSON.</summary>
        private static List<string> ToNullIfEmpty(List<string> source)
        {
            return (source == null || source.Count == 0) ? null : new List<string>(source);
        }

        /// <summary>
        /// Converte os pares do Inspector para o DTO de rede. Pares incompletos são
        /// descartados aqui — o servidor também os filtra, mas errar cedo evita mandar
        /// lixo na rede e deixa o aviso no Console de quem está autorando a cena.
        /// </summary>
        private static List<TaskItemPair> ToPairList(List<TaskPairData> source)
        {
            if (source == null || source.Count == 0) return null;

            var result = new List<TaskItemPair>(source.Count);
            foreach (var pair in source)
            {
                if (pair == null || string.IsNullOrWhiteSpace(pair.itemId) || string.IsNullOrWhiteSpace(pair.slotId))
                {
                    Debug.LogWarning("[GAMEPLAY] TaskSystemBridge — par item/slot incompleto no catálogo do Inspector, descartado.");
                    continue;
                }
                result.Add(new TaskItemPair { ItemId = pair.itemId, SlotId = pair.slotId });
            }
            return result.Count > 0 ? result : null;
        }
    }

    /// <summary>
    /// Par item→destino no Inspector. Existe como classe própria porque Dictionary não
    /// serializa no Inspector do Unity — o par precisa ser uma lista de objetos.
    /// </summary>
    [Serializable]
    public class TaskPairData
    {
        [Tooltip("itemId do CarryableItem que conta progresso.")]
        public string itemId;

        [Tooltip("slotId do TaskDepositPoint onde este item DEVE ser entregue.")]
        public string slotId;
    }

    /// <summary>
    /// Estrutura serializable usada no Inspector para definir o catálogo inicial.
    /// Espelha TaskEntry mas como classe separada para compatibilidade com o Inspector do Unity.
    /// Shape: { id, description, type, targetCount } — sem npcId, sem label.
    /// </summary>
    [Serializable]
    public class TaskEntryData
    {
        [Tooltip("Identificador único da tarefa (ex: 'task-collect-docs').")]
        public string id;

        [Tooltip("Descrição legível da tarefa (ex: 'Colete 3 documentos').")]
        public string description;

        [Tooltip("Categoria da tarefa (ex: 'collect', 'deliver', 'repair', 'escort').")]
        public string type;

        [Tooltip("Quantidade alvo para conclusão da tarefa.")]
        public int targetCount;

        [Tooltip("Opcional: itemIds que contam progresso nesta task. Vazio = qualquer entrega conta +1. " +
                 "Os ids precisam bater EXATAMENTE com o campo _itemId dos CarryableItem na cena.")]
        public List<string> items = new List<string>();

        [Tooltip("Opcional: destino obrigatório por item. Use quando entregar no lugar errado " +
                 "deve ser recusado (ex: encomenda na estante certa).")]
        public List<TaskPairData> pairs = new List<TaskPairData>();
    }
}
