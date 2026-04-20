using UnityEngine;

namespace HoraExtra.Network
{
    /**
     * NetworkSettings: Armazena dados de sessão e configurações globais de rede.
     */
    public static class NetworkSettings
    {
        private static string _authToken;

        /// <summary>
        /// O Token JWT real obtido via API REST (Login).
        /// </summary>
        public static string AuthToken
        {
            get => PlayerPrefs.GetString("AuthToken", _authToken);
            set
            {
                _authToken = value;
                PlayerPrefs.SetString("AuthToken", value);
                PlayerPrefs.Save();
            }
        }

        public static bool HasToken => !string.IsNullOrEmpty(AuthToken);

        public static void ClearToken()
        {
            _authToken = null;
            PlayerPrefs.DeleteKey("AuthToken");
        }
    }
}
