using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MissionPaperCollectible : MonoBehaviour
{
    [Header("Mission")]
    [SerializeField] private PaperMissionManager missionManager;

    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;

    private bool collected = false;
    private bool wasInRange = false;

    private void Awake()
    {
        if (missionManager == null)
        {
            missionManager = FindFirstObjectByType<PaperMissionManager>();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }
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

                if (missionManager != null)
                    missionManager.ShowInteractionText();

                Debug.Log("Player perto do documento: " + gameObject.name);
            }

            if (InteractPressed())
            {
                Collect();
            }
        }
        else
        {
            if (wasInRange)
            {
                wasInRange = false;

                if (missionManager != null)
                    missionManager.HideInteractionText();
            }
        }
    }

    private void TryFindPlayerAgain()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
            Debug.Log("Player encontrado pelo documento: " + gameObject.name);
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
        collected = true;
        wasInRange = false;

        if (missionManager != null)
        {
            missionManager.HideInteractionText();
            missionManager.CollectPaper();
        }
        else
        {
            Debug.LogWarning("Mission Manager não encontrado no documento: " + gameObject.name);
        }

        Debug.Log("Documento coletado: " + gameObject.name);

        gameObject.SetActive(false);
    }
}