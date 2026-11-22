using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HoraExtra.Characters;
using HoraExtra.Network.Models;
using HoraExtra.UI;

namespace HoraExtra.Dev
{
    /// <summary>
    /// Modo de teste rápido do Financeiro — SÓ no Editor e desligado por padrão.
    /// Liga/desliga em <c>Tools > Hora-Extra > Teste rápido: Financeiro</c> (fica salvo no
    /// EditorPrefs, nunca em cena nem em build).
    ///
    /// Com ele ligado, dando Play na SCN_FirstFloor:
    ///   - o catálogo enviado ao servidor tem só as tarefas do Financeiro (cofres primeiro,
    ///     e-mail depois — o servidor entrega a 1ª entrada primeiro e a outra em seguida);
    ///   - assim que a tarefa chega, o elevador leva o jogador direto para a SCN_ThirdFloor.
    /// </summary>
    public static class DevQuickTest
    {
        public const string PREF_KEY = "HoraExtra.TesteRapidoFinanceiro";
        public const string CENA_ALVO = "SCN_ThirdFloor";

        /// <summary>True só no Editor com o item de menu marcado.</summary>
        public static bool Ativo
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(PREF_KEY, false);
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Filtra o catálogo para só o Financeiro, com os cofres na frente. Se nenhuma entrada
        /// casar, devolve o catálogo original (não deixa a sala sem tarefa).
        /// </summary>
        public static List<TaskEntry> FiltrarCatalogo(List<TaskEntry> tasks)
        {
            if (!Ativo || tasks == null) return tasks;

            var filtrado = new List<TaskEntry>();
            foreach (var t in tasks)
                if (t.Type == TaskPresentation.TYPE_OPEN_SAFE_COLLECT_MONEY) filtrado.Add(t);
            foreach (var t in tasks)
                if (t.Type == TaskPresentation.TYPE_SEND_EMAIL_UPLOAD) filtrado.Add(t);

            if (filtrado.Count == 0) return tasks;

            Debug.LogWarning($"[GAMEPLAY] TESTE RÁPIDO (Financeiro) ligado — catálogo reduzido a {filtrado.Count} tarefa(s). " +
                             "Desligue em Tools > Hora-Extra > Teste rápido: Financeiro.");
            return filtrado;
        }

        private static bool _jaDesviou;

        /// <summary>
        /// Funciona tanto dando Play direto na SCN_FirstFloor quanto começando pelo menu: o
        /// desvio para o 3º andar acontece na primeira vez que a SCN_FirstFloor carrega.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Iniciar()
        {
            _jaDesviou = false;
            if (!Ativo) return;

            SceneManager.sceneLoaded -= AoCarregarCena;
            SceneManager.sceneLoaded += AoCarregarCena;
            AoCarregarCena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
        {
            if (_jaDesviou || cena.name != "SCN_FirstFloor") return;
            _jaDesviou = true;

            var go = new GameObject("DevQuickTest (runtime)");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>().StartCoroutine(IrParaOTerceiroAndar(go));
        }

        private static IEnumerator IrParaOTerceiroAndar(GameObject dono)
        {
            // Espera conectar e a tarefa chegar (máx. 20 s), para o catálogo já estar registrado.
            float limite = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < limite)
            {
                var bridge = TaskSystemBridge.Instance;
                if (bridge != null && bridge.GetMyTasks() != null && bridge.GetMyTasks().Count > 0
                    && ElevatorTransition.Instance != null)
                    break;
                yield return new WaitForSecondsRealtime(0.25f);
            }

            if (ElevatorTransition.Instance == null)
            {
                Debug.LogError("[GAMEPLAY] TESTE RÁPIDO — sem ElevatorTransition na cena; vá de elevador manualmente.");
            }
            else
            {
                Debug.Log($"[GAMEPLAY] TESTE RÁPIDO — indo direto para {CENA_ALVO}.");
                ElevatorTransition.Instance.GoToScene(CENA_ALVO);
            }

            Object.Destroy(dono, 1f);
        }

        private class Runner : MonoBehaviour { }
    }
}
