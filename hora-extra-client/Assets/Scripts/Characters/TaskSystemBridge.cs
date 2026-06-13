using System;
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

        // === Inspector ===
        [Header("Task Catalog — entradas iniciais da cena")]
        [Tooltip("Lista de tarefas que serão registradas no servidor ao conectar.")]
        [SerializeField] private List<TaskEntryData> _initialCatalog = new List<TaskEntryData>();

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

        // === Lifecycle ===

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Catálogo inicial: garantir entrada da cafeteira caso não configurada no Inspector.
                EnsureCoffeeMakerEntry();
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
        }

        private void OnDisable()
        {
            SocketManager.Instance?.Off(NetworkEvents.CONNECTION_SUCCESS, OnConnectionSuccess);
            SocketManager.Instance?.Off(NetworkEvents.TASK_ASSIGNED, OnTaskAssignedReceived);
            SocketManager.Instance?.Off(NetworkEvents.TASK_UPDATED, OnTaskUpdatedReceived);
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
            _catalogRegistered = false;
            _myTasks.Clear();
            RegisterCatalog();
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

                // Armazena tasks do jogador local.
                if (IsLocalPlayer(payload.PlayerId))
                {
                    _myTasks.Clear();
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
                    TargetCount = entry.targetCount
                });
            }

            var payload = new TaskCatalogRegisterPayload { Tasks = tasks };
            SocketManager.Instance.Emit(NetworkEvents.TASK_CATALOG_REGISTER, payload);
            _catalogRegistered = true;

            Debug.Log($"[NETWORK] task_catalog_register enviado — {tasks.Count} tarefa(s).");
        }
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
    }
}
