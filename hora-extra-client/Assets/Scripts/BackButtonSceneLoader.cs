using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButtonSceneLoader : MonoBehaviour
{
    [Header("Cena de destino")]
    [SerializeField] private string targetSceneName;

    public void GoToTargetScene()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("Nenhuma cena de destino foi definida no BackButtonSceneLoader.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning($"A cena '{targetSceneName}' não foi encontrada. Verifique se o nome está correto e se ela foi adicionada no Build Settings/Build Profiles.");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }
}