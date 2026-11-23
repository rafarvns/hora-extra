using System.Collections.Generic;
using HoraExtra.Network.Models;

namespace HoraExtra.UI
{
    /// <summary>
    /// Fonte única dos textos de apresentação das tarefas no cliente.
    ///
    /// Mapeia o <see cref="AssignedTask"/> cru (vindo do servidor) para conteúdo legível
    /// — título, instrução de "como fazer", rótulo de progresso e prompt de ação — usado
    /// pela HUD (card de missão, colegas, marcador de objetivo — ver <c>UI/Hud/</c>) e pelos
    /// prompts de interação.
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

        // Tarefa 16 — Organizar a recepção (plano 0005). Cada bullet do documento de arte
        // é uma entrada de catálogo própria, para o sorteio tratá-las como tarefas distintas.
        public const string TYPE_COLLECT_PAPERS    = "collect_papers";
        public const string TYPE_COLLECT_PENS      = "collect_pens";
        public const string TYPE_COLLECT_MAGAZINES = "collect_magazines";

        // Tarefa 18 — Limpar recepção e almoxarifado (plano 0006).
        public const string TYPE_CLEAN_FLOOR = "clean_floor";
        public const string TYPE_SWEEP_FLOOR = "sweep_floor";

        // Recepção e almoxarifado — latas na máquina de vendas e caixas na estante.
        public const string TYPE_RETURN_CANS   = "return_cans";
        public const string TYPE_SHELVE_BOXES  = "shelve_boxes";

        // Financeiro (3º andar) — Tarefa 6 (cofres) e Tarefa 7 (enviar e-mail).
        public const string TYPE_OPEN_SAFE_COLLECT_MONEY = "open_safe_collect_money";
        public const string TYPE_SEND_EMAIL_UPLOAD       = "send_email_upload";

        // === Status (campo 'status' autoritativo do servidor) ===
        public const string STATUS_PENDING     = "pending";
        public const string STATUS_IN_PROGRESS = "in_progress";
        public const string STATUS_COMPLETED   = "completed";
        public const string STATUS_FAILED      = "failed";

        /// <summary>Agrupamento visual das tarefas (cor e rótulo do badge na HUD).</summary>
        public enum TaskCategory { Organize, Clean, Coffee, Finance, Other }

        /// <summary>True para status terminais (concluída ou falhou).</summary>
        public static bool IsFinished(AssignedTask task)
        {
            return task != null && (task.Status == STATUS_COMPLETED || task.Status == STATUS_FAILED);
        }

        /// <summary>
        /// Primeira tarefa ainda aberta (pending/in_progress) na ordem em que foram liberadas,
        /// ou null. Com a fila sequencial do servidor, é "a tarefa atual".
        /// </summary>
        public static AssignedTask FindActive(IEnumerable<AssignedTask> tasks)
        {
            if (tasks == null) return null;
            foreach (var t in tasks)
                if (t != null && !IsFinished(t)) return t;
            return null;
        }

        public static TaskCategory GetCategory(string type)
        {
            switch (type)
            {
                case TYPE_COLLECT:
                case TYPE_COLLECT_PAPERS:
                case TYPE_COLLECT_PENS:
                case TYPE_COLLECT_MAGAZINES:
                case TYPE_RETURN_CANS:
                case TYPE_SHELVE_BOXES:      return TaskCategory.Organize;
                case TYPE_CLEAN_FLOOR:
                case TYPE_SWEEP_FLOOR:       return TaskCategory.Clean;
                case TYPE_COFFEE_MAKER:      return TaskCategory.Coffee;
                case TYPE_OPEN_SAFE_COLLECT_MONEY:
                case TYPE_SEND_EMAIL_UPLOAD: return TaskCategory.Finance;
                default:                     return TaskCategory.Other;
            }
        }

        public static string GetCategoryLabel(TaskCategory category)
        {
            switch (category)
            {
                case TaskCategory.Organize: return "ORGANIZAÇÃO";
                case TaskCategory.Clean:    return "LIMPEZA";
                case TaskCategory.Coffee:   return "COPA";
                case TaskCategory.Finance:  return "FINANCEIRO";
                default:                    return "TAREFA";
            }
        }

        /// <summary>
        /// Nome legível de uma categoria de item carregado (campo <c>_kind</c> do
        /// CarryableItem), no singular ou plural conforme <paramref name="count"/>.
        /// </summary>
        public static string GetItemLabel(string kind, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(kind)) return string.Empty;

            bool plural = count != 1;
            switch (kind)
            {
                case "papel":   return plural ? "Papéis" : "Papel";
                case "caneta":  return plural ? "Canetas" : "Caneta";
                case "revista": return plural ? "Revistas" : "Revista";
                case "rodo":    return "Rodo";
                case "vassoura": return "Vassoura";
                case "lata":    return plural ? "Latas" : "Lata";
                case "caixa":   return plural ? "Caixas" : "Caixa";
                default:        return char.ToUpper(kind[0]) + kind.Substring(1);
            }
        }

        /// <summary>
        /// Resumo curto para a linha do checklist — sempre o rótulo do tipo, nunca a
        /// descrição do catálogo.
        ///
        /// Separado do <see cref="GetTitle"/> de propósito: a descrição virou texto
        /// instrucional longo, que não cabe numa linha de lista. O passo a passo completo
        /// fica no <see cref="GetHowTo"/>, exibido só para a tarefa ativa.
        /// </summary>
        public static string GetSummary(AssignedTask task)
        {
            if (task == null) return string.Empty;

            switch (task.Type)
            {
                case TYPE_COFFEE_MAKER:      return "Preparar o café";
                case TYPE_COLLECT:           return "Coletar documentos";
                case TYPE_COLLECT_PAPERS:    return "Organizar os papéis";
                case TYPE_COLLECT_PENS:      return "Organizar as canetas";
                case TYPE_COLLECT_MAGAZINES: return "Organizar as revistas";
                case TYPE_CLEAN_FLOOR:       return "Limpar as manchas do chão";
                case TYPE_SWEEP_FLOOR:       return "Varrer a sujeira do chão";
                case TYPE_RETURN_CANS:       return "Levar as latas à máquina de vendas";
                case TYPE_SHELVE_BOXES:      return "Guardar as caixas na estante";
                case TYPE_OPEN_SAFE_COLLECT_MONEY: return "Abrir os cofres e recolher o dinheiro";
                case TYPE_SEND_EMAIL_UPLOAD: return "Enviar o e-mail pendente";
                default:
                    return string.IsNullOrWhiteSpace(task.Description) ? "Tarefa" : task.Description;
            }
        }

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
                case TYPE_COFFEE_MAKER:     return "Preparar o café";
                case TYPE_COLLECT:          return "Coletar documentos";
                case TYPE_COLLECT_PAPERS:   return "Organizar os papéis";
                case TYPE_COLLECT_PENS:     return "Organizar as canetas";
                case TYPE_COLLECT_MAGAZINES:return "Organizar as revistas";
                case TYPE_CLEAN_FLOOR:      return "Limpar as manchas do chão";
                case TYPE_SWEEP_FLOOR:      return "Varrer a sujeira do chão";
                case TYPE_RETURN_CANS:      return "Levar as latas à máquina de vendas";
                case TYPE_SHELVE_BOXES:     return "Guardar as caixas na estante";
                case TYPE_OPEN_SAFE_COLLECT_MONEY: return "Abrir os cofres e recolher o dinheiro";
                case TYPE_SEND_EMAIL_UPLOAD: return "Enviar o e-mail pendente";
                default:                    return "Tarefa";
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
                case TYPE_COLLECT_PAPERS:
                    return $"Há {task.TargetCount} papéis espalhados pelo chão da recepção.\n" +
                           "Aperte [E] para pegar — dá para juntar vários de uma vez.\n" +
                           "Leve até a bandeja em cima da mesa e aperte [E] para guardar.\n" +
                           "O losango dourado mostra onde entregar.";
                case TYPE_COLLECT_PENS:
                    return $"Há {task.TargetCount} canetas espalhadas pela recepção.\n" +
                           "Aperte [E] para pegar e leve até o porta-canetas, em cima da mesa.\n" +
                           "O losango dourado mostra onde entregar.";
                case TYPE_COLLECT_MAGAZINES:
                    return $"Há {task.TargetCount} revistas fora do lugar na recepção.\n" +
                           "Aperte [E] para pegar e coloque-as de volta na mesa de centro.\n" +
                           "O losango dourado mostra onde entregar.";
                case TYPE_CLEAN_FLOOR:
                    return $"Há {task.TargetCount} manchas de água e café pelo chão.\n" +
                           "Primeiro pegue o rodo com [E] — ele fica perto da entrada.\n" +
                           "Depois vá até cada mancha e SEGURE [E] até a barra encher.\n" +
                           "O rodo continua na sua mão: dá para limpar todas sem voltar.";
                case TYPE_SWEEP_FLOOR:
                    return $"Há {task.TargetCount} sujeiras de terra e de chips pelo chão.\n" +
                           "Primeiro pegue a vassoura com [E] (se estiver com o rodo, solte-o com [Q]).\n" +
                           "Depois vá até cada sujeira e SEGURE [E] até a barra encher.\n" +
                           "O losango dourado mostra a vassoura e depois cada sujeira.";
                case TYPE_RETURN_CANS:
                    return $"Há {task.TargetCount} latas largadas pelo andar.\n" +
                           "Aperte [E] para pegar — dá para juntar várias de uma vez.\n" +
                           "Leve até a máquina de vendas e aperte [E] para guardar.\n" +
                           "O losango dourado mostra onde entregar.";
                case TYPE_SHELVE_BOXES:
                    return $"Há {task.TargetCount} caixas de papelão fora do lugar no almoxarifado.\n" +
                           "Aperte [E] para pegar uma caixa e leve até a estante.\n" +
                           "Aperte [E] na estante para guardar. O losango dourado mostra onde.";
                case TYPE_OPEN_SAFE_COLLECT_MONEY:
                    // TEMPORÁRIO: senhas no texto porque os post-its estão ilegíveis na cena
                    // (cadeira na frente). Tirar quando a Alice reposicionar os post-its.
                    return "Na sala dos cofres do Financeiro, dois cofres estão trancados.\n" +
                           "Senhas: cofre da DIREITA = 1512 · cofre da ESQUERDA = 2803.\n" +
                           "Aperte [E] no cofre e digite a senha; com a porta aberta, aperte [E] de novo\n" +
                           "para recolher o dinheiro (cerca de 10 s parado). Repita no outro cofre.";
                case TYPE_SEND_EMAIL_UPLOAD:
                    return "Há um e-mail pendente de envio no Financeiro.\n" +
                           "Vá até o computador com a tela de e-mail e aperte [E].\n" +
                           "Clique em \"Enviar email\" e espere a barra encher sem fechar a tela.";
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
                case TYPE_SEND_EMAIL_UPLOAD:
                    return "Aperte [E] para usar o computador";
                default:
                    return "Pressione E para interagir";
            }
        }

        /// <summary>
        /// Prompt genérico de ENTREGA, exibido no recipiente (TaskDepositPoint) quando ele
        /// não tem uma mensagem própria configurada no Inspector.
        ///
        /// Fallback de propósito: o texto específico de cada tarefa ("Aperte [E] para guardar
        /// a caneta") é conteúdo de cena e entra nos planos 0005–0008. Aqui fica só o que
        /// serve para qualquer entrega, com o progresso atual para o jogador se situar.
        /// </summary>
        public static string GetDeliveryPrompt(AssignedTask task)
        {
            if (task == null) return "Aperte [E] para guardar";
            return $"Aperte [E] para guardar ({task.CurrentProgress}/{task.TargetCount})";
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
