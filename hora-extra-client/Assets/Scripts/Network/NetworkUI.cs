using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Gerencia a exibição visual do estado da rede e logs em tempo real na UI.
/// </summary>
public class NetworkUI : MonoBehaviour
{
        [Header("UI References")]
        [SerializeField] private Image pingImage;
        [SerializeField] private TextMeshProUGUI pingText;
        [SerializeField] private TextMeshProUGUI logDebug;

        [Header("Configurações")]
        [SerializeField] private int maxLogLines = 15;
        
        private List<string> _logLines = new List<string>();
        private StringBuilder _stringBuilder = new StringBuilder();

        private void Reset()
        {
            AutoCaptureReferences();
        }

        private void Awake()
        {
            if (pingImage == null || pingText == null || logDebug == null)
            {
                AutoCaptureReferences();
            }
        }

        private void AutoCaptureReferences()
        {
            if (pingImage == null) pingImage = transform.Find("pingImage")?.GetComponent<Image>();
            if (pingText == null) pingText = transform.Find("pingText")?.GetComponent<TextMeshProUGUI>();
            if (logDebug == null) logDebug = transform.Find("logDebug")?.GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void Start()
        {
            if (logDebug != null) logDebug.text = "<color=cyan>[SYSTEM] Log Debug Console Iniciado.</color>";
        }

        private void Update()
        {
            UpdatePingUI();
        }

        private void UpdatePingUI()
        {
            if (SocketManager.Instance == null || !SocketManager.Instance.IsConnected)
            {
                if (pingImage != null) pingImage.color = Color.gray;
                if (pingText != null) pingText.text = "OFFLINE";
                return;
            }

            float latency = SocketManager.Instance.Latency;
            if (pingText != null) pingText.text = $"{Mathf.RoundToInt(latency)} ms";

            if (pingImage != null)
            {
                if (latency < 80) pingImage.color = Color.green;
                else if (latency < 180) pingImage.color = new Color(1f, 0.6f, 0f); // Laranja suave
                else pingImage.color = Color.red;
            }
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (logDebug == null) return;

            string colorHeader = "";
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception: colorHeader = "<color=red>[ERROR]</color> "; break;
                case LogType.Warning: colorHeader = "<color=yellow>[WARN]</color> "; break;
                default: colorHeader = "<color=white>[LOG]</color> "; break;
            }

            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string formattedLine = $"[{timestamp}] {colorHeader}{logString}";
            
            _logLines.Add(formattedLine);

            if (_logLines.Count > maxLogLines)
            {
                _logLines.RemoveAt(0);
            }

            _stringBuilder.Clear();
            // Inverter a ordem se preferir os logs mais novos no topo, 
            // mas o usuário pediu para "ir subindo ou descendo", geralmente descendo é padrão.
            // Vamos manter a ordem cronológica (novos no final).
            for (int i = 0; i < _logLines.Count; i++)
            {
                _stringBuilder.AppendLine(_logLines[i]);
            }

            logDebug.text = _stringBuilder.ToString();
        }
    }
