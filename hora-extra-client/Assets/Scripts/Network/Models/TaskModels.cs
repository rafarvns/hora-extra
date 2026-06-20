using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HoraExtra.Network.Models
{
    /// <summary>
    /// Uma entrada do catálogo de tarefas definido pelo cliente.
    /// Shape: { id, description, type, targetCount } — sem npcId.
    /// Catálogo é da sala, não vinculado a NPC.
    /// </summary>
    [Serializable]
    public class TaskEntry
    {
        [JsonProperty("id")]          public string Id;
        [JsonProperty("description")] public string Description;
        [JsonProperty("type")]        public string Type;
        [JsonProperty("targetCount")] public int    TargetCount;
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
    /// Shape: { taskId: string }
    /// Cada envio soma +1 no progresso de uma task incremental (ex: 'collect').
    /// O servidor decide as transições pending → in_progress → completed.
    /// </summary>
    [Serializable]
    public class TaskProgressPayload
    {
        [JsonProperty("taskId")] public string TaskId;
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
