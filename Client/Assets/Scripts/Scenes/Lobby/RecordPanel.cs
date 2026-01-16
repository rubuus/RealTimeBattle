using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/* 
 * RecordPanel.cs
 * 
 * 역할 : 
 * - Record UI Panel 관리
 * 
*/

public class RecordPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text firstEnemy;
    [SerializeField] private TMP_Text firstResult;
    [SerializeField] private TMP_Text secondEnemy;
    [SerializeField] private TMP_Text secondResult;
    [SerializeField] private TMP_Text thirdEnemy;
    [SerializeField] private TMP_Text thirdResult;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private Button prevBtn;
    [SerializeField] private Button nextBtn;

    private TMP_Text[,] recordText = new TMP_Text[3, 2];
    private int pageNumber = 1;

    private void Start()
    {
        prevBtn.onClick.AddListener(() => {
            StartCoroutine(FoundRecord(-1));
        });

        nextBtn.onClick.AddListener(() => {
            StartCoroutine(FoundRecord(1));
        });

        recordText[0, 0] = firstEnemy;
        recordText[0, 1] = firstResult;
        recordText[1, 0] = secondEnemy;
        recordText[1, 1] = secondResult;
        recordText[2, 0] = thirdEnemy;
        recordText[2, 1] = thirdResult;
    }

    // Panel 활성화 시, page 1로 초기화 및 API로 전적 데이터 요청
    private void OnEnable()
    {
        pageNumber = 1;
        StartCoroutine(FoundRecord(0));
    }

    // Panel 비활성화 시, page 1로 초기화
    private void OnDisable()
    {
        pageNumber = 1;
    }

    // 정상적으로 전적 데이터 불러와지면, UI 표시
    private IEnumerator FoundRecord(int dir)
    {
        int userId = AuthManager.Instance.UserId;

        // 방향 설정 (next, prev)
        if (dir == 1) pageNumber++;
        else if (dir == -1)
        {
            if (pageNumber == 1)
                yield break;

            pageNumber--;
        }

        pageText.text = pageNumber.ToString();

        // API 전적 데이터 요청
        yield return API.Instance.SendJsonRequest<object>(
            endpoint : $"battle/{userId}?page={pageNumber}",
            method : UnityWebRequest.kHttpVerbGET,
            data : null,
            onSuccess: res => {
                var record = JsonConvert.DeserializeObject<List<BattleRecordResponse>>(res);

                foreach(var i in recordText)
                {
                    i.text = string.Empty;
                }
                    
                if (record.Count > 0)
                    RecordView(record, userId);
            },
            onError: err => Debug.LogError(err)
        );
    }

    // 전적 UI 표시
    private void RecordView(List<BattleRecordResponse> list, int id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            bool win = id == list[i].WinnerId;

            if (win)
            {
                recordText[i, 0].text = list[i].LoserNickname;
                recordText[i, 1].text = "WIN";
                recordText[i, 1].color = Color.red;
            }
            else
            {
                recordText[i, 0].text = list[i].WinnerNickname;
                recordText[i, 1].text = "LOSE";
                recordText[i, 1].color = Color.blue;
            }       
        }
    }
}
