using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    [SerializeField] private LoginManager loginManager;

    public string lastSceneName = string.Empty;

    private void Awake()
    {
        lastSceneName = SceneManager.GetActiveScene().name;

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
        lastSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadSceneAsync(targetSceneName);
    }

    public IEnumerator ViewSceneCoroutine(float fadeDuration)
    {
        yield return new WaitUntil(() => FadeManager.Instance.HasCanvasGroup);
        yield return FadeManager.Instance.FadeIn(fadeDuration);
    }
}
