using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverBold : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Texto do botão")]
    [SerializeField] private TextMeshProUGUI buttonText;

    private FontStyles originalFontStyle;

    private void Awake()
    {
        if (buttonText == null)
        {
            buttonText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (buttonText != null)
        {
            originalFontStyle = buttonText.fontStyle;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buttonText == null)
            return;

        buttonText.fontStyle = FontStyles.Bold;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (buttonText == null)
            return;

        buttonText.fontStyle = originalFontStyle;
    }

    private void OnDisable()
    {
        if (buttonText != null)
        {
            buttonText.fontStyle = originalFontStyle;
        }
    }
}