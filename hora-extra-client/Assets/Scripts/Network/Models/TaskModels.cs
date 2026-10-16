using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HoraExtra.Network.Models
{
    /// <summary>
    /// Par item→destino de uma tarefa de pareamento (ex: encomenda → estante correta).
    /// Shape: { itemId, slotId }
    /// </summary>
    [Serializable]
    public class TaskItemPair
    {
        [JsonProperty("itemId")] public string ItemId;
        [JsonProperty("slotId")] public string SlotId;
    }

    /// <summary>
    /// Uma entrada do catálogo de tarefas definido pelo cliente.
    /// Shape: { id, description, type, targetCount, items?, pairs? } — sem npcId.
    /// Catálogo é da sala, não vinculado a NPC.
    ///
    /// items/pairs são opcionais e dizem ao servidor QUAIS itens contam progresso nesta
    /// task e, no caso de pairs, em qual destino cada um deve ser entregue. Entrada que
    /// não declara nenhum dos dois mantém a contagem cega de +1 por task_progress.
    ///
    /// NullValueHandling.Ignore: sem isso o Newtonsoft mandaria "items": null na rede,
    /// e o servidor trataria o campo como declarado-porém-vazio.
    /// </summary>
    [Serializable]
    public class TaskEntry
    {
        [JsonProperty("id")]          public string Id;
        [JsonProperty("description")] public string Description;
        [JsonProperty("type")]        public string Type;
        [JsonProperty("targetCount")] public int    TargetCount;

        [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Items;

        [JsonProperty("pairs", NullValueHandling = NullValueHandling.Ignore)]
        public List<TaskItemPair> Pairs;
    }

    /// <summary>
    /// Payload enviado ao servidor via task_catalog_register (C→S).
    /// Shape: { tasks: Array<{ id, description, type, targetCount }> }
    /// </summary>
    [Serializable]
    public class TaskCatalogRegisterPayload
    {
        [JsonProperty("tasks")] public List<TaskEntry> Tasks;
    }

    /// <summary>
    /// Task atribuída ao jogador pelo servidor.
    /// Recebida dentro do broadcast task_assigned (S→C).
    /// Shape: { id, description, type, targetCount, currentProgress, status }
    ///
    /// Valores válidos de status:
    ///   "pending"     — atribuída, aguardando interação.
    ///   "in_progress" — interação iniciada via task_start_interaction.
    ///   "completed"   — minigame concluído com sucesso.
    ///   "failed"      — minigame falhou (terminal; não pode ser retentada).
    /// </summary>
    [Serializable]
    public class AssignedTask
    {
        [JsonProperty("id")]              public string Id;
        [JsonProperty("description")]     public string Description;
        [JsonProperty("type")]            public string Type;
        [JsonProperty("targetCount")]     public int    TargetCount;
        [JsonProperty("currentProgress")] public int    CurrentProgress;
        [JsonProperty("status")]          public string Status;
    }

    /// <summary>
    /// Payload recebido do servidor via task_assigned (S→C broadcast).
    /// Shape: { playerId: string, tasks: AssignedTask[] }
    /// </summary>
    [Serializable]
    public class TaskAssignedPayload
    {
        [JsonProperty("playerId")] public string           PlayerId;
        [JsonProperty("tasks")]    public List<AssignedTask> Tasks;
    }

    /// <summary>
    /// Payload enviado ao servidor via task_start_interaction (C→S).
    /// Shape: { taskId: string }
    /// Inicia transição de status: pending → in_progress.
    /// </summary>
    [Serializable]
    public class TaskStartInteractionPayload
    {
        [JsonProperty("taskId")] public string TaskId;
    }

    /// <summary>
    /// Payload enviado ao servidor via task_complete_attempt (C→S).
    /// Shape: { taskId: string, success: boolean }
    /// O servidor determina o status final autoritativamente (completed ou failed).
    /// </summary>
    [Serializable]
    public class TaskCompleteAttemptPayload
    {
        [JsonProperty("taskId")]  public string TaskId;
        [JsonProperty("success")] public bool   Success;
    }

    /// <summary>
    /// Payload enviado ao servidor via task_progress (C→S).
    /// Shape: { taskId: string, itemId?: string, slotId?: string }
    /// Cada envio soma +1 no progresso de uma task incremental (ex: 'collect').
    /// O servidor decide as transições pending → in_progress → completed.
    ///
    /// itemId identifica QUAL item foi entregue e slotId ONDE. São opcionais e só
    /// validados quando a entrada de catálogo declara items/pairs — o que permite às
    /// tasks antigas seguirem funcionando sem alteração.
    /// </summary>
    [Serializable]
    public class TaskProgressPayload
    {
        [JsonProperty("taskId")] public string TaskId;

        [JsonProperty("itemId", NullValueHandling = NullValueHandling.Ignore)]
        public string ItemId;

        [JsonProperty("slotId", NullValueHandling = NullValueHandling.Ignore)]
        public string SlotId;
    }

    /// <summary>
    /// Payload recebido do servidor via task_rejected (S→C unicast).
    /// Shape: { taskId: string, code: string, message: string, itemId?: string }
    ///
    /// Significa "a regra do jogo não permitiu" — o cliente deve desfazer o efeito
    /// otimista (devolver o item à mão) e exibir Message. Diferente do ERROR genérico,
    /// que indica pacote malformado e é bug de cliente.
    ///
    /// Valores de Code: NOT_ASSIGNED, INVALID_STATUS, MISSING_ITEM, UNKNOWN_ITEM,
    /// WRONG_SLOT, ALREADY_COUNTED.
    /// </summary>
    [Serializable]
    public class TaskRejectedPayload
    {
        [JsonProperty("taskId")]  public string TaskId;
        [JsonProperty("code")]    public string Code;
        [JsonProperty("message")] public string Message;
        [JsonProperty("itemId")]  public string ItemId;
    }

    /// <summary>
    /// Payload recebido do servidor via task_updated (S→C broadcast).
    /// Shape: { playerId: string, taskId: string, currentProgress: number, status: string }
    /// Construído pelo servidor — não reflete campos crus do cliente.
    /// </summary>
    [Serializable]
    public class TaskUpdatedPayload
    {
        [JsonProperty("playerId")]        public string PlayerId;
        [JsonProperty("taskId")]          public string TaskId;
        [JsonProperty("currentProgress")] public int    CurrentProgress;
        [JsonProperty("status")]          public string Status;
    }
}
