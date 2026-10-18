using TMPro;
using UnityEngine;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu.Sections
{
    /// <summary>Créditos: equipe (de documents/gdd.md), o projeto e as tecnologias.</summary>
    public class CreditsPanel : MenuPanel
    {
        protected override string Title => "Créditos";

        private static readonly (string nome, string funcao)[] EQUIPE =
        {
            ("Rafael Araújo",  "Programação geral"),
            ("Evans Farias",   "Interface de usuário"),
            ("Alice Matoso",   "Game design e produção"),
            ("Gerson Jarreta", "Modelagem 3D e personagens"),
            ("Emilly Vitória", "Modelagem 3D e personagens"),
        };

        protected override void BuildContent()
        {
            MenuFactory.SectionTitle(Content, Theme, "EQUIPE");
            foreach (var (nome, funcao) in EQUIPE)
                Member(nome, funcao);

            MenuFactory.SectionTitle(Content, Theme, "PROJETO");
            Paragraph("Hora-Extra — jogo multiplayer cooperativo de terror psicológico.\n" +
                      "Trabalho de Conclusão de Curso · PUC Minas.");

            MenuFactory.SectionTitle(Content, Theme, "FEITO COM");
            Paragraph("Unity 6  ·  Node.js  ·  TypeScript  ·  Prisma  ·  MySQL");
        }

        private void Member(string nome, string funcao)
        {
            var row = HudFactory.Rect($"Member_{nome}", Content);
            HudFactory.Row(row.gameObject, 12f, TextAnchor.MiddleLeft);
            HudFactory.Fixed(row.gameObject, -1f, 40f);

            var n = HudFactory.Label(row, "Name", Theme, Theme.BodySize * 1.1f, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            n.text = nome;
            HudFactory.Fixed(n.gameObject, 280f, 40f);

            var f = HudFactory.Label(row, "Role", Theme, Theme.BodySize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft);
            f.text = funcao;
        }

        private void Paragraph(string text)
        {
            var p = HudFactory.Label(Content, "Text", Theme, Theme.BodySize, Theme.TextMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal, wrap: true);
            p.lineSpacing = 8f;
            p.text = text;
        }
    }
}
