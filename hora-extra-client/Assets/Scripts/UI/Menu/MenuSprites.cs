using UnityEngine;

namespace HoraExtra.UI.Menu
{
    /// <summary>
    /// Texturas do fundo do menu geradas em runtime (gradiente vertical, vinheta, brilho de
    /// lâmpada). Complementa <see cref="HoraExtra.UI.Hud.HudSprites"/>; nada aqui depende de arte.
    /// </summary>
    public static class MenuSprites
    {
        private static Sprite _vignette;
        private static Sprite _topGlow;

        /// <summary>Gradiente vertical de <paramref name="bottom"/> (base) a <paramref name="top"/>.</summary>
        public static Sprite VerticalGradient(Color top, Color bottom)
        {
            const int h = 256;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
            {
                name = "MENU_Gradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < h; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (h - 1f)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Vinheta: transparente no centro, opaca nas bordas (pinte de preto).</summary>
        public static Sprite Vignette
        {
            get
            {
                if (_vignette != null) return _vignette;
                const int s = 256;
                var tex = NewTexture(s, s, "MENU_Vignette");
                var c = new Vector2(s / 2f, s / 2f);
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (s * 0.5f);   // 0 centro, ~1.41 cantos
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.35f, d));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();
                return _vignette = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>Brilho que desce do topo (luz fluorescente do teto): opaco em cima, some embaixo.</summary>
        public static Sprite TopGlow
        {
            get
            {
                if (_topGlow != null) return _topGlow;
                const int w = 128, h = 128;
                var tex = NewTexture(w, h, "MENU_TopGlow");
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float vertical = Mathf.Pow(y / (h - 1f), 2.2f);                  // forte no topo
                    float horizontal = 1f - Mathf.Abs(x / (w - 1f) - 0.5f) * 2f;      // forte no meio
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, vertical * Mathf.SmoothStep(0f, 1f, horizontal)));
                }
                tex.Apply();
                return _topGlow = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        private static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }
    }
}
