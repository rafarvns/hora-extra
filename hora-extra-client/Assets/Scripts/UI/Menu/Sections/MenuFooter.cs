using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using HoraExtra.Network;
using HoraExtra.Settings;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu.Sections
{
    /// <summary>
    /// Rodapé: pílula de status do servidor (esquerda) e versão do jogo (direita).
    ///
    /// O health check é feito aqui direto, com timeout curto e sem log a cada tentativa —
    /// o HealthService/ApiClient logam erro em toda falha, o que inundaria o Console com o
    /// servidor fora do ar. O resultado é publicado em <see cref="MainMenuRoot.ReportServerStatus"/>.
    /// </summary>
    public class MenuFooter : MenuWidget
    {
        private const float CHECK_SECONDS = 5f;
        private const int TIMEOUT_SECONDS = 3;

        private Image _dot;
        private TextMeshProUGUI _status;
        private Coroutine _loop;
        private string _checkedHost;

        protected override void Build()
        {
            HudFactory.Stretch(Root);

            var pill = HudFactory.Panel(Root, "ServerPill", HudTheme.WithAlpha(Theme.Panel, 0.8f), Theme);
            pill.pixelsPerUnitMultiplier = Theme.CornerSharpness * 2f;
            HudFactory.Place(pill.rectTransform, Vector2.zero, new Vector2(140f, 44f), new Vector2(200f, 40f));
            HudFactory.Row(pill.gameObject, 10f, TextAnchor.MiddleLeft, new RectOffset(16, 18, 6, 6));
            HudFactory.FitContent(pill.gameObject, true, false);

            _dot = HudFactory.Dot(pill.transform, "Dot", Theme.TextMuted, 12f);
            _status = HudFactory.Label(pill.transform, "Status", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft);
            _status.text = "Verificando servidor…";

            var version = HudFactory.Label(Root, "Version", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.Right);
            HudFactory.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 50f), new Vector2(600f, 30f));
            version.text = $"v{Application.version}  ·  TCC  ·  PUC Minas";
        }

        private void OnEnable()
        {
            GameSettings.OnChanged += CheckNow;
            _loop = StartCoroutine(Loop());
        }

        private void OnDisable()
        {
            GameSettings.OnChanged -= CheckNow;
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
        }

        /// <summary>Troca de servidor nas Configurações → checa na hora em vez de esperar o ciclo.</summary>
        private void CheckNow()
        {
            if (!isActiveAndEnabled || BackendConfig.Host == _checkedHost) return;
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return Check();
                yield return new WaitForSecondsRealtime(CHECK_SECONDS);
            }
        }

        private IEnumerator Check()
        {
            string host = BackendConfig.Host;
            _checkedHost = host;
            float inicio = Time.realtimeSinceStartup;

            using (var req = UnityWebRequest.Get($"{BackendConfig.ApiBaseUrl}/health"))
            {
                req.timeout = TIMEOUT_SECONDS;
                yield return req.SendWebRequest();

                // O jogador trocou de servidor durante a requisição: descarta o resultado velho.
                if (host != BackendConfig.Host) yield break;

                bool online = req.result == UnityWebRequest.Result.Success;
                float ms = (Time.realtimeSinceStartup - inicio) * 1000f;
                Show(online, ms);
                Menu.ReportServerStatus(online, ms);
            }
        }

        private void Show(bool online, float ms)
        {
            var preset = BackendConfig.CurrentPreset;
            string nome = preset.HasValue ? preset.Value.Label : BackendConfig.Host;

            _dot.color = online ? Theme.Success : Theme.Danger;
            _status.color = online ? Theme.Text : Theme.Danger;
            _status.text = online
                ? $"Servidor online  <color={HudFactory.Hex(Theme.TextMuted)}>·  {Mathf.RoundToInt(ms)} ms  ·  {nome}</color>"
                : $"Servidor offline  <color={HudFactory.Hex(Theme.TextMuted)}>·  {nome}</color>";
        }
    }
}
