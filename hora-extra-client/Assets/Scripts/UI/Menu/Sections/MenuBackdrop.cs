using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu.Sections
{
    /// <summary>
    /// Fundo do menu: "o expediente acabou e você ainda está no escritório".
    ///   - gradiente azul-marinho escuro;
    ///   - faixas de luz de persiana inclinadas, deslizando bem devagar;
    ///   - brilho de lâmpada fluorescente no teto que, de vez em quando, falha;
    ///   - relógio de parede grande passando das 18:00 (ponteiro de segundos "tiquetaqueia");
    ///   - vinheta escura nas bordas.
    /// Tudo gerado em código — nenhum asset de arte.
    /// </summary>
    public class MenuBackdrop : MenuWidget
    {
        private const int BLIND_COUNT = 7;
        private const float START_MINUTE = 18 * 60 + 7;   // 18:07
        private const float GAME_MINUTES_PER_SECOND = 1f;

        private RectTransform _blinds;
        private Image _glow;
        private float _glowBase;
        private float _nextFlickerAt;
        private float _flickerUntil;
        private int _flickerBlinksLeft;

        private RectTransform _hourHand;
        private RectTransform _minuteHand;
        private RectTransform _secondHand;
        private float _startTime;

        protected override void Build()
        {
            HudFactory.Stretch(Root);

            var bg = Root.gameObject.AddComponent<Image>();
            bg.sprite = MenuSprites.VerticalGradient(new Color32(0x16, 0x20, 0x33, 0xFF), new Color32(0x07, 0x0B, 0x14, 0xFF));
            bg.raycastTarget = false;

            BuildBlinds();
            BuildGlow();
            BuildClock();

            var vignette = HudFactory.Rect("Vignette", Root).gameObject.AddComponent<Image>();
            HudFactory.Stretch(vignette.rectTransform);
            vignette.sprite = MenuSprites.Vignette;
            vignette.color = new Color(0f, 0f, 0f, 0.85f);
            vignette.raycastTarget = false;

            _startTime = Time.unscaledTime;
            _nextFlickerAt = Time.unscaledTime + Random.Range(2f, 5f);
        }

        private void BuildBlinds()
        {
            _blinds = HudFactory.Rect("Blinds", Root);
            HudFactory.Place(_blinds, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3200f, 1800f));
            _blinds.localRotation = Quaternion.Euler(0f, 0f, -16f);

            for (int i = 0; i < BLIND_COUNT; i++)
            {
                var stripe = HudFactory.Rect($"Stripe{i}", _blinds).gameObject.AddComponent<Image>();
                stripe.raycastTarget = false;
                stripe.color = new Color(0.75f, 0.82f, 1f, Random.Range(0.025f, 0.06f));
                float altura = Random.Range(60f, 120f);
                HudFactory.Place(stripe.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 420f - i * 150f), new Vector2(3200f, altura));
            }
        }

        private void BuildGlow()
        {
            _glow = HudFactory.Rect("CeilingGlow", Root).gameObject.AddComponent<Image>();
            _glow.sprite = MenuSprites.TopGlow;
            _glow.raycastTarget = false;
            _glowBase = 0.10f;
            _glow.color = new Color(0.85f, 0.92f, 1f, _glowBase);
            var rt = _glow.rectTransform;
            rt.anchorMin = new Vector2(0.1f, 0.45f);
            rt.anchorMax = new Vector2(0.9f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void BuildClock()
        {
            const float size = 560f;
            var clock = HudFactory.Rect("WallClock", Root);
            HudFactory.Place(clock, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            clock.anchorMin = clock.anchorMax = new Vector2(0.74f, 0.44f);

            var face = HudFactory.Dot(clock, "Face", new Color(0.05f, 0.08f, 0.13f, 0.75f), size);
            HudFactory.Stretch(face.rectTransform);

            var ring = HudFactory.Rect("Ring", clock).gameObject.AddComponent<Image>();
            ring.sprite = HudSprites.Ring;
            ring.color = HudTheme.WithAlpha(Theme.Text, 0.16f);
            ring.raycastTarget = false;
            HudFactory.Stretch(ring.rectTransform, -6f, -6f, -6f, -6f);

            // 12 marcações; as de 3 em 3 horas mais grossas.
            for (int h = 0; h < 12; h++)
            {
                var pivot = HudFactory.Rect($"TickPivot{h}", clock);
                HudFactory.Place(pivot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                pivot.localRotation = Quaternion.Euler(0f, 0f, -30f * h);

                bool forte = h % 3 == 0;
                var tick = HudFactory.Rect("Tick", pivot).gameObject.AddComponent<Image>();
                tick.raycastTarget = false;
                tick.color = HudTheme.WithAlpha(Theme.Text, forte ? 0.45f : 0.22f);
                HudFactory.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.40f),
                    new Vector2(forte ? 8f : 4f, forte ? 34f : 20f));
            }

            _hourHand = Hand(clock, "Hour", size * 0.24f, 12f, HudTheme.WithAlpha(Theme.Text, 0.7f));
            _minuteHand = Hand(clock, "Minute", size * 0.36f, 7f, HudTheme.WithAlpha(Theme.Text, 0.7f));
            _secondHand = Hand(clock, "Second", size * 0.40f, 3f, HudTheme.WithAlpha(Theme.Accent, 0.9f));

            var hub = HudFactory.Dot(clock, "Hub", Theme.Accent, 22f);
            HudFactory.Place(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        }

        /// <summary>Ponteiro: retângulo com pivô na base, girando em torno do centro do relógio.</summary>
        private static RectTransform Hand(RectTransform clock, string name, float length, float width, Color color)
        {
            var img = HudFactory.Rect(name, clock).gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.08f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, length);
            return rt;
        }

        private void Update()
        {
            float agora = Time.unscaledTime;

            // Persianas: deriva lenta para o fundo não parecer uma imagem parada.
            _blinds.anchoredPosition = new Vector2(Mathf.Sin(agora * 0.05f) * 60f, Mathf.Sin(agora * 0.07f) * 30f);

            UpdateClock(agora);
            UpdateFlicker(agora);
        }

        private void UpdateClock(float agora)
        {
            float decorrido = agora - _startTime;
            float minutoDoDia = START_MINUTE + decorrido * GAME_MINUTES_PER_SECOND;
            float minutos = minutoDoDia % 60f;
            float horas = (minutoDoDia / 60f) % 12f;

            _minuteHand.localRotation = Quaternion.Euler(0f, 0f, -6f * minutos);
            _hourHand.localRotation = Quaternion.Euler(0f, 0f, -30f * horas);
            // Segundos em degraus: o "tique" do relógio.
            _secondHand.localRotation = Quaternion.Euler(0f, 0f, -6f * Mathf.Floor(decorrido % 60f));
        }

        /// <summary>A lâmpada falha de tempos em tempos: 1 a 3 piscadas rápidas.</summary>
        private void UpdateFlicker(float agora)
        {
            if (_flickerBlinksLeft == 0 && agora >= _nextFlickerAt)
            {
                _flickerBlinksLeft = Random.Range(1, 4) * 2;   // apaga/acende
                _flickerUntil = agora;
            }

            if (_flickerBlinksLeft > 0 && agora >= _flickerUntil)
            {
                _flickerBlinksLeft--;
                bool apagada = _flickerBlinksLeft % 2 == 1;
                SetGlow(apagada ? _glowBase * 0.15f : _glowBase);
                _flickerUntil = agora + Random.Range(0.05f, 0.12f);
                if (_flickerBlinksLeft == 0)
                {
                    SetGlow(_glowBase);
                    _nextFlickerAt = agora + Random.Range(4f, 11f);
                }
            }
        }

        private void SetGlow(float alpha)
        {
            var c = _glow.color;
            c.a = alpha;
            _glow.color = c;
        }
    }
}
