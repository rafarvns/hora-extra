using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Characters;
using HoraExtra.Network.Models;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Documento coletável da missão "Colete os documentos".
///
/// Versão server-authoritative: ao coletar, NÃO altera estado local — envia
/// task_progress ao servidor via TaskSystemBridge. O servidor soma +1 no progresso
/// da task de tipo "collect" e faz broadcast de task_updated, que o MissionHud
/// consome para atualizar o contador. O papel só some da cena após o envio.
///
/// Substitui a antiga dependência de PaperMissionManager (lógica local removida).
/// </summary>
public class MissionPaperCollectible : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;

    [Tooltip("Texto opcional de prompt (ex: 'Pressione E para coletar'). Pode ficar vazio.")]
    [SerializeField] private Text interactionText;

    [Tooltip("Mensagem exibida no prompt quando o player está perto.")]
    [SerializeField] private string interactionMessage = "Pressione E para coletar";

    private bool collected = false;
    private bool wasInRange = false;

    private void Awake()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (collected)
            return;

        if (player == null)
        {
            TryFindPlayerAgain();
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        bool isInRange = distance <= interactDistance;

        if (isInRange)
        {
            if (!wasInRange)
            {
                wasInRange = true;
                ShowPrompt();
                Debug.Log("[GAMEPLAY] Player perto do documento: " + gameObject.name);
            }

            if (InteractPressed())
                Collect();
        }
        else if (wasInRange)
        {
            wasInRange = false;
            HidePrompt();
        }
    }

    private void TryFindPlayerAgain()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            Debug.Log("[GAMEPLAY] Player encontrado pelo documento: " + gameObject.name);
        }
    }

    private bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
            return true;
#endif

        return false;
    }

    private void Collect()
    {
        TaskSystemBridge bridge = TaskSystemBridge.Instance;
        if (bridge == null)
        {
            Debug.LogWarning("[GAMEPLAY] TaskSystemBridge ausente — documento não pôde ser coletado: " + gameObject.name);
            return;
        }

        // Localiza a task de coleta atribuída ao jogador local (pending ou já em andamento).
        AssignedTask task = bridge.FindMyTask(
            TaskSystemBridge.PAPER_COLLECT_TYPE, "pending", "in_progress");

        if (task == null)
        {
            Debug.LogWarning("[GAMEPLAY] Nenhuma task 'collect' ativa para o jogador — documento ignorado: " + gameObject.name);
            return;
        }

        collected = true;
        wasInRange = false;
        HidePrompt();

        // Envio autoritativo: o servidor soma o progresso e faz broadcast task_updated.
        bridge.SendProgress(task.Id);

        Debug.Log("[GAMEPLAY] Documento coletado (task_progress enviado): " + gameObject.name);
        gameObject.SetActive(false);
    }

    private void ShowPrompt()
    {
        if (interactionText == null)
            return;
        interactionText.text = interactionMessage;
        interactionText.gameObject.SetActive(true);
    }

    private void HidePrompt()
    {
        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }
}
