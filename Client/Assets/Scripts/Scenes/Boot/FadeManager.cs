using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
 * FadeManager.cs
 * 
 * 역할 :
 * - 씬 이동 시, Fade 효과 처리
 * 
 */

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    [SerializeField] private Image backColor;
    [SerializeField] private float defaultFadeDuration = 0.5f;
    private CanvasGroup fadeCanvasGroup;
    private Coroutine currentCouroutine;

    public bool HasCanvasGroup => fadeCanvasGroup != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Matching" || scene.name == "Battle" || scene.name == "Result")
        {
            backColor.gameObject.SetActive(false);
        }
        else
        {
            backColor.gameObject.SetActive(true);
        }

       StartCoroutine(FindCanvasGroupDelayed());
    }

    private IEnumerator FindCanvasGroupDelayed()
    {
        // 씬의 Start()들이 다 실행된 다음 프레임까지 대기
        yield return null;
        yield return new WaitForEndOfFrame();
        fadeCanvasGroup = FindFirstObjectByType<CanvasGroup>(FindObjectsInactive.Include);

        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning($"[FadeManager] CanvasGroup not found in scene '{SceneManager.GetActiveScene().name}'!");
        }
        else
        {
            Debug.Log($"[FadeManager] CanvasGroup reconnected in '{SceneManager.GetActiveScene().name}'");
        }
    }

    // Black -> White
    public IEnumerator FadeIn(float duration = -1f)
    {
        if (fadeCanvasGroup == null) yield break;
        yield return currentCouroutine = StartCoroutine(FadeCoroutine(0f, 1f, duration < 0 ? defaultFadeDuration : duration));
    }

    // White -> Black
    public IEnumerator FadeOut(float duration = -1f)
    {
        if (fadeCanvasGroup == null) yield break;
        yield return currentCouroutine = StartCoroutine(FadeCoroutine(1f, 0f, duration < 0 ? defaultFadeDuration : duration));
    }

    // Fade 로직
    private IEnumerator FadeCoroutine(float from, float to, float duration)
    {
        if (fadeCanvasGroup == null) yield break;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, time / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, time / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
    }
}
