using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchingTime : MonoBehaviour
{
    [SerializeField] private CanvasGroup uiCanvas;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text guideText;

    private Coroutine timerCoroutine;

    private int elapsedSeconds = 0;

    private void OnEnable()
    {
        // 패널이 켜질 때 한 번만 코루틴 시작
        if (timerCoroutine == null)
            timerCoroutine = StartCoroutine(FindMatch());
    }

    private void OnDisable()
    {
        // 패널이 꺼질 때 코루틴 종료
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
    }

    private IEnumerator FindMatch()
    {
        elapsedSeconds = 0;
        timeText.text = "00:00";
        guideText.text = "Finding Match";

        while (true)
        {
            yield return new WaitForSeconds(1f);
            elapsedSeconds++;

            // TimeSpan으로 변환
            TimeSpan timeSpan = TimeSpan.FromSeconds(elapsedSeconds);

            // 00:00:00 형식으로 표시
            timeText.text = timeSpan.ToString(@"mm\:ss");

            if (elapsedSeconds % 8 == 0)
                guideText.text = "Finding Match";
            else if (elapsedSeconds % 2 == 0)
                guideText.text += ".";

            if (elapsedSeconds > 10)
                SuccessMatching();
        }
    }

    public void SuccessMatching()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        // 유저 입력 차단
        if (uiCanvas != null)
        {
            uiCanvas.interactable = false;
            uiCanvas.blocksRaycasts = false;
        }

        guideText.text = "Fight Now!!";
        StartCoroutine(WaitAndLoad());
    }

    private IEnumerator WaitAndLoad()
    {
        yield return new WaitForSeconds(1f);
        StartCoroutine(SceneLoader.Instance.LoadSceneCoroutine("Matching", 1f));
    }
}
