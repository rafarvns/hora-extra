using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ElevatorTransition : MonoBehaviour
{
    public static ElevatorTransition Instance;

    [SerializeField] CanvasGroup blackScreen;   // Image preta em tela cheia dentro de um Canvas
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip elevatorSound;   // 1º som: elevador subindo/descendo
    [SerializeField] AudioClip arrivalSound;    // 2º som: ex. "ding" de chegada
    [SerializeField] float fadeTime = 0.5f;

    bool busy;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        blackScreen.alpha = 0f;
        blackScreen.blocksRaycasts = false;

        EnsureElevatorFloor(SceneManager.GetActiveScene());
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureElevatorFloor(scene);
    }

    public void GoToScene(string sceneName)
    {
        if (!busy) StartCoroutine(Routine(sceneName));
    }

    IEnumerator Routine(string sceneName)
    {
        busy = true;
        blackScreen.blocksRaycasts = true; // impede clicar em coisas durante a transição

        // 1. Vai ficando tudo escuro
        yield return Fade(1f);

        // 2. Toca o som do elevador
        audioSource.PlayOneShot(elevatorSound);

        // 3. Após 2 segundos...
        yield return new WaitForSeconds(2f);

        // 4. Carrega a nova cena e reposiciona o jogador perto do elevador de lá
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        yield return op;
        yield return null; // espera um frame pra cena nova "assentar"

        RepositionPlayerNearElevator();

        // 5. Clareia a tela
        yield return Fade(0f);

        if (arrivalSound != null) audioSource.PlayOneShot(arrivalSound);

        blackScreen.blocksRaycasts = false;
        busy = false;
    }

    IEnumerator Fade(float target)
    {
        float start = blackScreen.alpha;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            blackScreen.alpha = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        blackScreen.alpha = target;
    }

    /// <summary>
    /// A cena recém-carregada só tem a posição genérica de spawn do Player, que não
    /// fica perto do elevador dela. Encontra o modelo do elevador na cena ativa e
    /// reposiciona o jogador ao lado dele.
    /// </summary>
    void RepositionPlayerNearElevator()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Transform elevator = FindElevatorTransform();
        if (elevator == null) return;

        var pc = player.GetComponent<HoraExtra.Characters.PlayerController>();
        pc?.Teleport(elevator.position + Vector3.up * 1f);
    }

    static Transform FindElevatorTransform()
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("elevator") || n.Contains("elevador")) return t;
        }
        return null;
    }

    /// <summary>
    /// O modelo 3D do elevador (elevator.obj) foi importado sem collider (addColliders: 0)
    /// e nenhuma das cenas tem um collider físico manual na cabine — só o trigger do botão.
    /// Isso fazia o jogador cair pelo mapa ao entrar na região. Gera uma fatia fina de
    /// BoxCollider sólido só no piso da cabine (não a bounding box inteira do modelo, que
    /// incluiria paredes/teto e selaria a cabine, impedindo o jogador de entrar), em
    /// coordenadas mundiais (evita lidar com a escala 0.009 do modelo), de forma idempotente
    /// a cada cena carregada.
    /// </summary>
    static void EnsureElevatorFloor(Scene scene)
    {
        const float floorThickness = 0.2f;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("elevator") && !n.Contains("elevador")) continue;
                if (t.name == "ElevatorFloor_Generated") continue;

                bool hasSolidCollider = false;
                foreach (var c in t.GetComponentsInChildren<Collider>(true))
                {
                    if (!c.isTrigger) { hasSolidCollider = true; break; }
                }
                if (hasSolidCollider) continue;

                var renderers = t.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;

                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                var floor = new GameObject("ElevatorFloor_Generated");
                floor.transform.SetParent(t, true); // mantém posição mundial
                floor.transform.position = new Vector3(b.center.x, b.min.y + floorThickness * 0.5f, b.center.z);
                floor.AddComponent<BoxCollider>().size = new Vector3(b.size.x, floorThickness, b.size.z);
                Debug.Log($"[GAMEPLAY] Piso sólido gerado para '{t.name}' em {floor.transform.position}, size {new Vector3(b.size.x, floorThickness, b.size.z)}");
            }
        }
    }
}
