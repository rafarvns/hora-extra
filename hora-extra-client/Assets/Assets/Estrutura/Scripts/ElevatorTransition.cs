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
    }

    public void GoToScene(string sceneName)
    {
        if (!busy) StartCoroutine(Routine(sceneName));
    }

    IEnumerator Routine(string sceneName)
    {
        busy = true;
        blackScreen.blocksRaycasts = true; // impede clicar em coisas durante a transição

        // 1. Escurece a tela
        yield return Fade(1f);

        // 2. Toca o som do elevador e espera acabar
        audioSource.PlayOneShot(elevatorSound);
        yield return new WaitForSeconds(elevatorSound.length);

        // 2b. Toca o segundo som (ex: "ding" de chegada) e espera acabar
        audioSource.PlayOneShot(arrivalSound);
        yield return new WaitForSeconds(arrivalSound.length);

        // 3. Carrega a nova cena (o jogador já começa dentro do elevador nela)
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        yield return op;
        yield return null; // espera um frame pra cena nova "assentar"

        // 4. Clareia a tela
        yield return Fade(0f);

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
}