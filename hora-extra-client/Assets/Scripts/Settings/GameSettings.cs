using System;
using UnityEngine;
using HoraExtra.Network;

namespace HoraExtra.Settings
{
    /// <summary>
    /// Preferências do jogador, persistidas em PlayerPrefs e aplicadas no boot.
    ///
    /// Fonte única para o menu (Configurações / Apelido) e para quem consome os valores
    /// em jogo (PlayerController, BackgroundMusic, SocketManager). Quem precisa reagir a
    /// mudanças em tempo real assina <see cref="OnChanged"/>.
    /// </summary>
    public static class GameSettings
    {
        public const int NICKNAME_MAX_LENGTH = 16;

        private const string KEY_NICKNAME = "settings.nickname";
        private const string KEY_MASTER = "settings.masterVolume";
        private const string KEY_MUSIC = "settings.musicVolume";
        private const string KEY_SENSITIVITY = "settings.mouseSensitivity";
        private const string KEY_FULLSCREEN = "settings.fullscreen";
        private const string KEY_RESOLUTION = "settings.resolution";
        private const string KEY_SERVER = "settings.serverPreset";

        /// <summary>Disparado depois de qualquer alteração (já salva e aplicada).</summary>
        public static event Action OnChanged;

        /// <summary>Apelido exibido para os colegas. Vazio = nome aleatório.</summary>
        public static string Nickname
        {
            get => PlayerPrefs.GetString(KEY_NICKNAME, string.Empty);
            set
            {
                string limpo = (value ?? string.Empty).Trim();
                if (limpo.Length > NICKNAME_MAX_LENGTH) limpo = limpo.Substring(0, NICKNAME_MAX_LENGTH);
                SetString(KEY_NICKNAME, limpo);
            }
        }

        /// <summary>Volume geral (AudioListener), 0..1.</summary>
        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KEY_MASTER, 1f);
            set { SetFloat(KEY_MASTER, Mathf.Clamp01(value)); AudioListener.volume = MasterVolume; }
        }

        /// <summary>Multiplicador do volume das trilhas (BackgroundMusic), 0..1.</summary>
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KEY_MUSIC, 1f);
            set => SetFloat(KEY_MUSIC, Mathf.Clamp01(value));
        }

        /// <summary>Multiplicador da sensibilidade do mouse em jogo (1 = padrão do prefab).</summary>
        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(KEY_SENSITIVITY, 1f);
            set => SetFloat(KEY_SENSITIVITY, Mathf.Clamp(value, 0.1f, 3f));
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            set { SetInt(KEY_FULLSCREEN, value ? 1 : 0); ApplyDisplay(); }
        }

        /// <summary>"LxA" da resolução escolhida, ou vazio para manter a atual.</summary>
        public static string Resolution
        {
            get => PlayerPrefs.GetString(KEY_RESOLUTION, string.Empty);
            set { SetString(KEY_RESOLUTION, value ?? string.Empty); ApplyDisplay(); }
        }

        /// <summary>Servidor escolhido (ver <see cref="BackendConfig.Presets"/>).</summary>
        public static string ServerPreset
        {
            get => PlayerPrefs.GetString(KEY_SERVER, string.Empty);
            set { SetString(KEY_SERVER, value ?? string.Empty); BackendConfig.ApplyPreset(ServerPreset); }
        }

        /// <summary>Nome a mandar no handshake: conta logada → apelido → null (o chamador sorteia).</summary>
        public static string ResolvePlayerName()
        {
            var session = SessionManager.Instance;
            if (session != null && session.IsLoggedIn && session.CurrentPlayer != null
                && !string.IsNullOrWhiteSpace(session.CurrentPlayer.Nome))
                return session.CurrentPlayer.Nome;

            return string.IsNullOrWhiteSpace(Nickname) ? null : Nickname;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnBoot()
        {
            AudioListener.volume = MasterVolume;
            BackendConfig.ApplyPreset(ServerPreset);
            // No Editor a "tela" é a Game View; mexer em resolução lá só atrapalha.
            if (!Application.isEditor) ApplyDisplay();
        }

        private static void ApplyDisplay()
        {
            if (Application.isEditor) return;

            bool cheia = Fullscreen;
            if (TryParseResolution(Resolution, out int w, out int h))
                Screen.SetResolution(w, h, cheia ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            else
                Screen.fullScreen = cheia;
        }

        public static bool TryParseResolution(string value, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrEmpty(value)) return false;
            var partes = value.Split('x');
            return partes.Length == 2 && int.TryParse(partes[0], out width) && int.TryParse(partes[1], out height)
                   && width > 0 && height > 0;
        }

        private static void SetString(string key, string value) { PlayerPrefs.SetString(key, value); Save(); }
        private static void SetFloat(string key, float value) { PlayerPrefs.SetFloat(key, value); Save(); }
        private static void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); Save(); }

        private static void Save()
        {
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
