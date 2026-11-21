using UnityEngine;

namespace HoraExtra.UI.Hud
{
    /// <summary>
    /// Sprites da HUD gerados em runtime (retângulo arredondado 9-slice, círculo, anel).
    ///
    /// O projeto não tem arte de UI; gerar aqui evita depender de assets e mantém os cantos
    /// nítidos em qualquer resolução. Cada sprite é criado uma única vez e reaproveitado.
    /// </summary>
    public static class HudSprites
    {
        private const int ROUNDED_SIZE = 64;
        private const int ROUNDED_RADIUS = 24;
        private const int CIRCLE_SIZE = 128;

        private static Sprite _rounded;
        private static Sprite _circle;
        private static Sprite _ring;

        /// <summary>Retângulo de cantos arredondados, com borda de 9-slice (use Image.Type.Sliced).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null)
                {
                    var tex = NewTexture(ROUNDED_SIZE, "HUD_Rounded");
                    float r = ROUNDED_RADIUS;
                    for (int y = 0; y < ROUNDED_SIZE; y++)
                    for (int x = 0; x < ROUNDED_SIZE; x++)
                    {
                        // Distância até o retângulo interno (encolhido pelo raio): fora dele é canto.
                        float px = x + 0.5f, py = y + 0.5f;
                        float dx = Mathf.Max(r - px, px - (ROUNDED_SIZE - r), 0f);
                        float dy = Mathf.Max(r - py, py - (ROUNDED_SIZE - r), 0f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                    }
                    tex.Apply();
                    _rounded = Sprite.Create(tex, new Rect(0, 0, ROUNDED_SIZE, ROUNDED_SIZE), new Vector2(0.5f, 0.5f),
                        100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                    _rounded.name = "HUD_Rounded";
                }
                return _rounded;
            }
        }

        /// <summary>Círculo cheio (pontos de status, keycaps redondos, fundo de marcador).</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = CreateDisc("HUD_Circle", 0f));

        /// <summary>Anel (use com Image.Type.Filled radial para barra circular de progresso).</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = CreateDisc("HUD_Ring", 0.72f));

        private static Sprite CreateDisc(string name, float innerRatio)
        {
            var tex = NewTexture(CIRCLE_SIZE, name);
            float c = CIRCLE_SIZE / 2f;
            float outer = c - 1f;
            float inner = outer * innerRatio;
            for (int y = 0; y < CIRCLE_SIZE; y++)
            for (int x = 0; x < CIRCLE_SIZE; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(outer - d + 0.5f);
                if (innerRatio > 0f) a *= Mathf.Clamp01(d - inner + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, CIRCLE_SIZE, CIRCLE_SIZE), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        private static Texture2D NewTexture(int size, string name)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }
    }
}
