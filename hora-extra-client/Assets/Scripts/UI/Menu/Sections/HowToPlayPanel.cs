using TMPro;
using UnityEngine;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu.Sections
{
    /// <summary>Como jogar: objetivo, controles (keycaps) e uma leitura rápida da HUD.</summary>
    public class HowToPlayPanel : MenuPanel
    {
        protected override string Title => "Como jogar";

        protected override void BuildContent()
        {
            MenuFactory.SectionTitle(Content, Theme, "OBJETIVO");
            Paragraph("Você é estagiário num escritório que não deixa ninguém ir embora. Cumpra as tarefas do " +
                      "expediente junto com seus colegas antes que o relógio passe das 18:00 — e não chame a " +
                      "atenção do chefe.");

            MenuFactory.SectionTitle(Content, Theme, "CONTROLES");
            Control("W A S D", "Andar");
            Control("Mouse", "Olhar ao redor");
            Control("Shift", "Correr (gasta fôlego)");
            Control("E", "Interagir — segure para limpar");
            Control("Q", "Soltar o que está na mão");
            Control("Tab", "Ver todas as tarefas");
            Control("F3", "Console de debug");

            MenuFactory.SectionTitle(Content, Theme, "NA TELA");
            Paragraph("<b>Relógio</b> no topo: a hora do expediente.  <b>Card</b> à esquerda: sua tarefa atual e o " +
                      "passo a passo.  <b>Marcador</b> âmbar: para onde ir.  <b>Equipe</b> à direita: o que cada " +
                      "colega está fazendo.  <b>Na mão</b>, embaixo: o que você carrega (até 6 itens iguais).");
        }

        private void Paragraph(string text)
        {
            var p = HudFactory.Label(Content, "Text", Theme, Theme.BodySize, Theme.TextMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal, wrap: true);
            p.lineSpacing = 8f;
            p.text = text;
        }

        private void Control(string keys, string action)
        {
            var row = HudFactory.Rect($"Control_{keys}", Content);
            HudFactory.Row(row.gameObject, 8f, TextAnchor.MiddleLeft);
            HudFactory.Fixed(row.gameObject, -1f, 40f);

            var caps = HudFactory.Rect("Keys", row);
            HudFactory.Row(caps.gameObject, 6f, TextAnchor.MiddleLeft);
            HudFactory.Fixed(caps.gameObject, 230f, 40f);
            foreach (var key in keys.Split(' '))
                HudFactory.KeyCap(caps, key, Theme, 34f);

            var label = HudFactory.Label(row, "Action", Theme, Theme.BodySize, Theme.Text, TextAlignmentOptions.MidlineLeft);
            label.text = action;
        }
    }
}
