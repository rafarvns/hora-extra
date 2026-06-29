using HoraExtra.Network.Models;

namespace HoraExtra.UI
{
    /// <summary>
    /// Fonte única dos textos de apresentação das tarefas no cliente.
    ///
    /// Mapeia o <see cref="AssignedTask"/> cru (vindo do servidor) para conteúdo legível
    /// — título, instrução de "como fazer", rótulo de progresso e prompt de ação — usado
    /// pelo painel de missões (<see cref="MissionListHud"/>), pelos marcadores de mundo
    /// (<see cref="HoraExtra.Characters.WorldTaskMarker"/>) e pelos prompts de interação.
    ///
    /// Centraliza também as constantes de <c>type</c> e <c>status</c> que antes eram
    /// redeclaradas em cada script. NENHUM dado de rede novo: tudo deriva dos campos já
    /// enviados em task_assigned / task_updated.
    /// </summary>
    public static class TaskPresentation
    {
        // === Tipos de tarefa (campo 'type' do catálogo) ===
        public const string TYPE_COFFEE_MAKER = "coffee_maker";
        public const string TYPE_COLLECT      = "collect";

        // === Status (campo 'status' autoritativo do servidor) ===
        public const string STATUS_PENDING     = "pending";
        public const string STATUS_IN_PROGRESS = "in_progress";
        public const string STATUS_COMPLETED   = "completed";
        public const string STATUS_FAILED      = "failed";

        /// <summary>
        /// Título curto da tarefa para a lista de missões. Usa a descrição do catálogo
        /// como base; cai num rótulo por tipo caso a descrição venha vazia.
        /// </summary>
        public static string GetTitle(AssignedTask task)
        {
            if (task == null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(task.Description))
                return task.Description;

            switch (task.Type)
            {
                case TYPE_COFFEE_MAKER: return "Preparar o café";
                case TYPE_COLLECT:      return "Coletar documentos";
                default:                return "Tarefa";
            }
        }

        /// <summary>
        /// Instrução de COMO executar a tarefa — o coração desta feature. Explicada por tipo.
        /// </summary>
        public static string GetHowTo(AssignedTask task)
        {
            if (task == null) return string.Empty;

            switch (task.Type)
            {
                case TYPE_COFFEE_MAKER:
                    return "Vá até a cafeteira e pressione E para preparar o café no minigame.";
                case TYPE_COLLECT:
                    return $"Colete os {task.TargetCount} documentos espalhados pelo escritório " +
                           "(pressione E ao chegar perto de cada um).";
                default:
                    return "Aproxime-se do objetivo e pressione E para interagir.";
            }
        }

        /// <summary>
        /// Rótulo de progresso "X/Y". Para tipos sem contagem incremental ainda assim
        /// reflete o targetCount como meta.
        /// </summary>
        public static string GetProgressLabel(AssignedTask task)
        {
            if (task == null) return string.Empty;
            return $"{task.CurrentProgress}/{task.TargetCount}";
        }

        /// <summary>
        /// Texto curto exibido no prompt de mundo quando o jogador está próximo do objeto.
        /// Inclui o progresso nas tarefas de coleta.
        /// </summary>
        public static string GetActionPrompt(AssignedTask task)
        {
            if (task == null) return "Pressione E para interagir";

            switch (task.Type)
            {
                case TYPE_COFFEE_MAKER:
                    return "Pressione E para preparar o café";
                case TYPE_COLLECT:
                    return $"Pressione E para coletar documento ({task.CurrentProgress}/{task.TargetCount})";
                default:
                    return "Pressione E para interagir";
            }
        }

        /// <summary>
        /// Rótulo legível do status para exibir na lista de missões.
        /// </summary>
        public static string GetStatusLabel(AssignedTask task)
        {
            if (task == null) return string.Empty;

            switch (task.Status)
            {
                case STATUS_PENDING:     return "Pendente";
                case STATUS_IN_PROGRESS: return "Em andamento";
                case STATUS_COMPLETED:   return "Concluída";
                case STATUS_FAILED:      return "Falhou";
                default:                 return task.Status;
            }
        }
    }
}
