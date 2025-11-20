using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultManager : MonoBehaviour
{
    [SerializeField] private TMP_Text resultMessage;

    private void OnEnable()
    {
        StartCoroutine(ProcessResultFlow());
    }

    private IEnumerator ProcessResultFlow()
    {
        yield return null; // 1프레임 대기 (씬 로드 안정화)

        string resultText = string.Empty;

        // -----------------------
        // 1) 상대가 먼저 종료 → 자동 승리
        // -----------------------
        if (SocketClient.Instance.enemyDisconnected)
        {
            resultText = "Enemy is disconnected\nWin";
        }
        else
        {
            // -----------------------
            // 2) 정상 종료 → HP 비교
            // -----------------------
            int myHp = PlayerHUD.Instance.myHp;
            int enemyHp = PlayerHUD.Instance.enemyHp;

            if (myHp > enemyHp)
                resultText = "Win";
            else if (myHp < enemyHp)
                resultText = "Lose";
            else
                resultText = "Draw";
        }

        resultMessage.text = resultText;


        // 서버에 게임 종료 알림
        SocketClient.Instance.Send("GAME_END");

        // 서버 연결 해제
        SocketClient.Instance.Disconnect();

        // 3초 후 로비로 이동
        yield return new WaitForSeconds(3f);

        StartCoroutine(SceneLoader.Instance.LoadSceneCoroutine("Lobby", 0.5f));
    }
}
