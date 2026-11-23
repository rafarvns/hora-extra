using UnityEngine;

namespace HoraExtra.UI
{
    /// <summary>
    /// LEGADO — o menu antigo (uGUI montado na cena) que levava a Login/Cadastro/Lobby.
    /// Contas foram removidas: o menu atual é o <see cref="Menu.MainMenuRoot"/>, que monta a
    /// tela de salas em runtime. Este componente só continua existindo para a MainMenuScene
    /// não ficar com "Missing Script"; ele se desliga sozinho.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.SetActive(false);
        }
    }
}
