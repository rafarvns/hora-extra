using UnityEngine;
using HoraExtra.Interactions;

public class ElevatorButton : MonoBehaviour
{
    [SerializeField] string targetScene;        // nome da cena do outro andar
    [SerializeField] GameObject promptUI;       // texto "Aperte E para subir"

    bool playerInside;

    void Start()
    {
        if (promptUI != null) promptUI.SetActive(false);
        Debug.Log("ElevatorButton iniciado em: " + gameObject.name);
    }

    void Update()
    {
        // InteractionInput ([E]) funciona com o Input System novo, que é o único ativo no
        // projeto — Input.GetKeyDown lançava InvalidOperationException ao entrar no elevador.
        if (playerInside && InteractionInput.InteractPressed())
        {
            Debug.Log("E apertado, chamando transição");
            SetInside(false);

            if (ElevatorTransition.Instance == null)
            {
                Debug.LogError("Não existe ElevatorTransition ativo na cena!");
                return;
            }
            ElevatorTransition.Instance.GoToScene(targetScene);
        }
    }

    // ---- 3D ----
    void OnTriggerEnter(Collider other) { Enter(other.gameObject); }
    void OnTriggerExit(Collider other) { Exit(other.gameObject); }

    // ---- 2D ----
    void OnTriggerEnter2D(Collider2D other) { Enter(other.gameObject); }
    void OnTriggerExit2D(Collider2D other) { Exit(other.gameObject); }

    void Enter(GameObject other)
    {
        Debug.Log("Algo entrou: " + other.name + " | tag: " + other.tag);
        if (other.CompareTag("Player")) SetInside(true);
    }

    void Exit(GameObject other)
    {
        if (other.CompareTag("Player")) SetInside(false);
    }

    void SetInside(bool value)
    {
        playerInside = value;
        if (promptUI != null) promptUI.SetActive(value);
    }
}