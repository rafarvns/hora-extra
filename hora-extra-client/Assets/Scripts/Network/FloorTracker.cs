using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HoraExtra.Network
{
    /// <summary>
    /// Em que andar o jogador local está, derivado da cena ativa. O andar vai em todo
    /// player_move (campo <c>f</c>) para o servidor e os colegas saberem quem está onde —
    /// só aparece no mundo quem está no mesmo andar.
    /// </summary>
    public static class FloorTracker
    {
        public const string SCENE_FLOOR_1 = "SCN_FirstFloor";
        public const string SCENE_FLOOR_2 = "SCN_SecondFloor";
        public const string SCENE_FLOOR_3 = "SCN_ThirdFloor";

        /// <summary>1, 2 ou 3; 0 fora de um andar (menu).</summary>
        public static int CurrentFloor { get; private set; }

        /// <summary>Disparado depois de carregar a cena de um andar (já com CurrentFloor novo).</summary>
        public static event Action<int> OnFloorChanged;

        public static int FloorOf(string sceneName)
        {
            switch (sceneName)
            {
                case SCENE_FLOOR_1: return 1;
                case SCENE_FLOOR_2: return 2;
                case SCENE_FLOOR_3: return 3;
                default: return 0;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentFloor = 0;
            OnFloorChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= AoCarregarCena;
            SceneManager.sceneLoaded += AoCarregarCena;
        }

        private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
        {
            if (modo != LoadSceneMode.Single) return;
            CurrentFloor = FloorOf(cena.name);
            OnFloorChanged?.Invoke(CurrentFloor);
        }
    }
}
