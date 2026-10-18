using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using HoraExtra.Network;
using HoraExtra.Settings;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu.Sections
{
    /// <summary>
    /// Configurações: áudio, controles, vídeo e servidor. Cada controle grava em
    /// <see cref="GameSettings"/>, que salva e aplica na hora.
    /// </summary>
    public class SettingsPanel : MenuPanel
    {
        protected override string Title => "Configurações";

        private readonly List<string> _resolutionValues = new List<string>();

        protected override void BuildContent()
        {
            MenuFactory.SectionTitle(Content, Theme, "ÁUDIO");
            var master = SliderRow("Volume geral", 0f, 1f, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent);
            SliderRow("Música", 0f, 1f, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent);

            MenuFactory.SectionTitle(Content, Theme, "CONTROLES");
            SliderRow("Sensibilidade do mouse", 0.1f, 3f, GameSettings.MouseSensitivity,
                v => GameSettings.MouseSensitivity = v, v => $"{v:0.0}x");

            MenuFactory.SectionTitle(Content, Theme, "VÍDEO");
            var fullRow = MenuFactory.FieldRow(Content, Theme, "Tela cheia");
            MenuFactory.Toggle(fullRow, Theme, GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);

            var resRow = MenuFactory.FieldRow(Content, Theme, "Resolução");
            var resOptions = BuildResolutionOptions(out int resIndex);
            MenuFactory.Cycler(resRow, Theme, resOptions, resIndex, i => GameSettings.Resolution = _resolutionValues[i]);

            if (Application.isEditor)
                Note("No Editor, tela cheia e resolução só valem na build.");

            MenuFactory.SectionTitle(Content, Theme, "SERVIDOR");
            var serverRow = MenuFactory.FieldRow(Content, Theme, "Conectar em");
            var labels = new List<string>();
            int serverIndex = 0;
            for (int i = 0; i < BackendConfig.Presets.Length; i++)
            {
                labels.Add(BackendConfig.Presets[i].Label);
                if (BackendConfig.Presets[i].Host == BackendConfig.Host) serverIndex = i;
            }
            MenuFactory.Cycler(serverRow, Theme, labels, serverIndex, i => GameSettings.ServerPreset = BackendConfig.Presets[i].Id);
            Note("Vale a partir da próxima conexão. O status no rodapé atualiza na hora.");

            FirstSelected = master.gameObject;
        }

        private UnityEngine.UI.Slider SliderRow(string label, float min, float max, float value,
            Action<float> onChanged, Func<float, string> format)
        {
            var row = MenuFactory.FieldRow(Content, Theme, label);
            TextMeshProUGUI valueLabel = null;
            var slider = MenuFactory.Slider(row, Theme, min, max, value, v =>
            {
                onChanged(v);
                if (valueLabel != null) valueLabel.text = format(v);
            });
            valueLabel = HudFactory.Label(row, "Value", Theme, Theme.BodySize, Theme.TextMuted, TextAlignmentOptions.MidlineRight);
            valueLabel.text = format(value);
            HudFactory.Fixed(valueLabel.gameObject, 80f, 48f);
            return slider;
        }

        private void Note(string text)
        {
            var note = HudFactory.Label(Content, "Note", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic, wrap: true);
            note.text = text;
        }

        /// <summary>Resoluções únicas (L×A) do monitor; a primeira opção mantém a atual.</summary>
        private List<string> BuildResolutionOptions(out int selected)
        {
            var labels = new List<string> { $"Atual ({Screen.width}×{Screen.height})" };
            _resolutionValues.Clear();
            _resolutionValues.Add(string.Empty);

            var vistos = new HashSet<string>();
            foreach (var r in Screen.resolutions)
            {
                string valor = $"{r.width}x{r.height}";
                if (!vistos.Add(valor)) continue;
                _resolutionValues.Add(valor);
                labels.Add($"{r.width} × {r.height}");
            }

            selected = Mathf.Max(0, _resolutionValues.IndexOf(GameSettings.Resolution));
            return labels;
        }

        private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
    }
}
