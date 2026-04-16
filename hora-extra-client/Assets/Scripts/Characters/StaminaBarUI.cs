using UnityEngine;
using HoraExtra.Characters;

public class StaminaBarUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private RectTransform _staminaFill;

    private float _initialWidth;

    private void Start()
    {
        if (_staminaFill == null)
        {
            Debug.LogError("StaminaBarUI: Stamina Fill não foi atribuído no Inspector.");
            return;
        }

        if (_playerController == null)
        {
            _playerController = FindObjectOfType<PlayerController>();
        }

        _initialWidth = _staminaFill.sizeDelta.x;
    }

    private void Update()
    {
        if (_playerController == null || _staminaFill == null)
            return;

        float staminaPercent = _playerController.CurrentStamina / _playerController.MaxStamina;
        staminaPercent = Mathf.Clamp01(staminaPercent);

        Vector2 newSize = _staminaFill.sizeDelta;
        newSize.x = _initialWidth * staminaPercent;
        _staminaFill.sizeDelta = newSize;
    }
}