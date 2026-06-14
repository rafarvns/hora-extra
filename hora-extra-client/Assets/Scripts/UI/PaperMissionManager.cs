using UnityEngine;
using UnityEngine.UI;

public class PaperMissionManager : MonoBehaviour
{
    [Header("Mission Settings")]
    [SerializeField] private int totalPapers = 4;

    [Header("UI")]
    [SerializeField] private Text missionCounterText;
    [SerializeField] private Text interactionText;
    [SerializeField] private Text feedbackText;

    [Header("Feedback")]
    [SerializeField] private float feedbackDuration = 2f;

    private int collectedPapers = 0;
    private float feedbackTimer = 0f;
    private bool missionCompleted = false;

    private void Start()
    {
        UpdateMissionText();

        if (interactionText != null)
            interactionText.gameObject.SetActive(false);

        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (feedbackText != null && feedbackText.gameObject.activeSelf && !missionCompleted)
        {
            feedbackTimer -= Time.deltaTime;

            if (feedbackTimer <= 0f)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }
    }

    public void ShowInteractionText()
    {
        if (interactionText == null)
            return;

        interactionText.text = "Pressione E para coletar";
        interactionText.gameObject.SetActive(true);
    }

    public void HideInteractionText()
    {
        if (interactionText == null)
            return;

        interactionText.gameObject.SetActive(false);
    }

    public void CollectPaper()
    {
        if (missionCompleted)
            return;

        collectedPapers++;

        if (collectedPapers > totalPapers)
            collectedPapers = totalPapers;

        UpdateMissionText();

        if (collectedPapers >= totalPapers)
        {
            CompleteMission();
        }
        else
        {
            ShowFeedback("Documento coletado! " + collectedPapers + "/" + totalPapers);
        }
    }

    private void UpdateMissionText()
    {
        if (missionCounterText == null)
            return;

        missionCounterText.text =
            "Missão: Coletar documentos\n" +
            "Documentos: " + collectedPapers + "/" + totalPapers;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText == null)
            return;

        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);
        feedbackTimer = feedbackDuration;
    }

    private void CompleteMission()
    {
        missionCompleted = true;

        if (missionCounterText != null)
        {
            missionCounterText.text =
                "Missão concluída!\n" +
                "Documentos: " + collectedPapers + "/" + totalPapers;
        }

        if (feedbackText != null)
        {
            feedbackText.text = "Missão concluída!\nTodos os documentos foram coletados.";
            feedbackText.gameObject.SetActive(true);
        }

        Debug.Log("Missão concluída: todos os documentos foram coletados.");
    }
}