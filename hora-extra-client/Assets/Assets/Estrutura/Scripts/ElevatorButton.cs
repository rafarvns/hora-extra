using System;
using System.Text;
using UnityEngine;
using HoraExtra.Interactions;

public class ElevatorButton : MonoBehaviour
{
    /// <summary>Um andar de destino do elevador.</summary>
    [Serializable]
    public class Destino
    {
        [Tooltip("Texto do andar no prompt (ex: \"3º andar\").")]
        public string rotulo;

        [Tooltip("Nome da cena do andar (ex: SCN_ThirdFloor). Precisa estar no Build Settings.")]
        public string cena;

        [Tooltip("Tecla numérica que leva a este andar quando há mais de um destino (ex: 3).")]
        public int tecla;
    }

    [Tooltip("Andares que este painel alcança. Com um só, [E] leva até ele; com vários, a tecla numérica de cada um.")]
    [SerializeField] Destino[] destinos = new Destino[0];

    [Header("Legado (usado se 'destinos' estiver vazio)")]
    [SerializeField] string targetScene;        // nome da cena do outro andar
    [SerializeField] GameObject promptUI;       // texto "Aperte E para subir"

    InteractionPrompt prompt;
    bool playerInside;

    void Start()
    {
        if (promptUI != null) promptUI.SetActive(false);
        prompt = FindFirstObjectByType<InteractionPrompt>();
        Debug.Log("ElevatorButton iniciado em: " + gameObject.name);
    }

    void OnDisable()
    {
        if (prompt != null) prompt.Hide(this);
    }

    void Update()
    {
        if (!playerInside || PlayerActionLock.IsBusy) return;

        // InteractionInput ([E]) funciona com o Input System novo, que é o único ativo no
        // projeto — Input.GetKeyDown lançava InvalidOperationException ao entrar no elevador.
        if (destinos == null || destinos.Length <= 1)
        {
            if (InteractionInput.InteractPressed())
                Ir(destinos != null && destinos.Length == 1 ? destinos[0].cena : targetScene);
            return;
        }

        if (InteractionInput.DigitPressed(out int tecla))
        {
            foreach (var d in destinos)
            {
                if (d != null && d.tecla == tecla)
                {
                    Ir(d.cena);
                    return;
                }
            }
        }
    }

    void Ir(string cena)
    {
        if (string.IsNullOrWhiteSpace(cena))
        {
            Debug.LogError($"ElevatorButton '{name}' sem cena de destino configurada.");
            return;
        }

        Debug.Log($"Elevador: indo para {cena}");
        SetInside(false);

        if (ElevatorTransition.Instance == null)
        {
            Debug.LogError("Não existe ElevatorTransition ativo na cena!");
            return;
        }
        ElevatorTransition.Instance.GoToScene(cena);
    }

    string TextoDoPrompt()
    {
        if (destinos == null || destinos.Length == 0) return "Aperte [E] para usar o elevador";
        if (destinos.Length == 1)
        {
            string rotulo = string.IsNullOrWhiteSpace(destinos[0].rotulo) ? "o outro andar" : destinos[0].rotulo;
            return $"Aperte [E] para ir ao {rotulo}";
        }

        var sb = new StringBuilder("Elevador:  ");
        for (int i = 0; i < destinos.Length; i++)
        {
            if (destinos[i] == null) continue;
            if (i > 0) sb.Append("   ");
            sb.Append($"[{destinos[i].tecla}] {destinos[i].rotulo}");
        }
        return sb.ToString();
    }

    // ---- 3D ----
    void OnTriggerEnter(Collider other) { Enter(other.gameObject); }
    void OnTriggerExit(Collider other) { Exit(other.gameObject); }

    // ---- 2D ----
    void OnTriggerEnter2D(Collider2D other) { Enter(other.gameObject); }
    void OnTriggerExit2D(Collider2D other) { Exit(other.gameObject); }

    void Enter(GameObject other)
    {
        if (other.CompareTag("Player")) SetInside(true);
    }

    void Exit(GameObject other)
    {
        if (other.CompareTag("Player")) SetInside(false);
    }

    void SetInside(bool value)
    {
        playerInside = value;
        // Cena com o texto legado autorado (1º andar) continua usando só ele.
        if (promptUI != null)
        {
            promptUI.SetActive(value);
            return;
        }

        if (prompt == null) prompt = FindFirstObjectByType<InteractionPrompt>();
        if (prompt == null) return;
        if (value) prompt.Show(this, TextoDoPrompt());
        else prompt.Hide(this);
    }
}
