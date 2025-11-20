using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    [SerializeField] private LoginManager loginManager;

    public string currentSceneName = string.Empty;
    public string nextSceneName = string.Empty;

    private void Awake()
    {
        currentSceneName = SceneManager.GetActiveScene().name;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoginCanvasClick()
    {
        if (loginManager.loadScenePossible)
        {
            StartCoroutine(LoadSceneCoroutine("Lobby", 0.5f));
        }
    }

    public IEnumerator LoadSceneCoroutine(string targetSceneName, float fadeDuration)
    {
        yield return FadeManager.Instance.FadeOut(fadeDuration);
        SceneManager.LoadSceneAsync(targetSceneName);
    }

    public IEnumerator ViewSceneCoroutine(float fadeDuration)
    {
        yield return new WaitUntil(() => FadeManager.Instance.HasCanvasGroup);
        currentSceneName = SceneManager.GetActiveScene().name;
        yield return FadeManager.Instance.FadeIn(fadeDuration);
    }
}
