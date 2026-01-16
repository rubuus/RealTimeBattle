using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/*
 * SceneLoader.cs
 * 
 * 역할 :
 * - Scene Load 관리
 * 
 */

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    public string lastSceneName = string.Empty;

    [SerializeField] private LoginManager loginManager;

    // 싱글톤 초기화
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

    // 로그인이 됐으면, 0.5초 후에 Lobby Scene 로드
    public void LoginCanvasClick()
    {
        if (loginManager.loadScenePossible)
            StartCoroutine(LoadSceneCoroutine("Lobby", 0.5f));
    }

    // 3초 후, Battle Scene 로드
    public IEnumerator LoadBattle()
    {
        yield return new WaitForSeconds(3f);
        SceneManager.LoadScene("Battle");
    }

    // Fade Out 후, 다음 씬 로드
    public IEnumerator LoadSceneCoroutine(string targetSceneName, float fadeDuration)
    {
        yield return FadeManager.Instance.FadeOut(fadeDuration);
        lastSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadSceneAsync(targetSceneName);
    }

    // 씬 넘어간 후, Fade In
    public IEnumerator ViewSceneCoroutine(float fadeDuration)
    {
        yield return new WaitUntil(() => FadeManager.Instance.HasCanvasGroup);
        yield return FadeManager.Instance.FadeIn(fadeDuration);
    }
}
