using System;
using System.Collections;
using TMPro;
using UnityEngine;

/* 
 * MatchingTime.cs
 * 
 * 역할 : 
 * - 매칭 대기 중 경과 시간 UI 표시
 * 
*/

public class MatchingTime : MonoBehaviour
{
    [SerializeField] private CanvasGroup uiCanvas;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text guideText;

    private Coroutine timerCoroutine;

    private int elapsedSeconds = 0;

    // Panel 활성화 시, 한 번만 코루틴 시작
    private void OnEnable()
    {
        if (timerCoroutine == null)
            timerCoroutine = StartCoroutine(FindMatch());
    }

    // Panel 비활성화 시, 코루틴 종료
    private void OnDisable()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
    }

    // 매칭 대기 UI 업데이트 코루틴
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

            // 00:00 형식으로 표시
            timeText.text = timeSpan.ToString(@"mm\:ss");

            // 2초마다 . 추가, 8초마다 text 초기화
            if (elapsedSeconds % 8 == 0)
                guideText.text = "Finding Match";
            else if (elapsedSeconds % 2 == 0)
                guideText.text += ".";
        }
    }

    // 매칭 성공 시, UI 업데이트
    public void SuccessMatching()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        if (uiCanvas != null)
        {
            uiCanvas.interactable = false;
            uiCanvas.blocksRaycasts = false;
        }

        guideText.text = "Fight Now!!";
        StartCoroutine(WaitAndLoad());
    }

    // 1초 후, Matching 씬 이동 (0.5초 FadeOut)
    private IEnumerator WaitAndLoad()
    {
        yield return new WaitForSeconds(1f);
        StartCoroutine(SceneLoader.Instance.LoadSceneCoroutine("Matching", 0.5f));
    }
}
