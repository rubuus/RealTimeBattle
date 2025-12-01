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

        if (SocketClient.Instance.enemyDisconnected)
        {
            resultText = "Enemy is disconnected\nWin";
        }
        else
        {
            string result = SocketClient.Instance.finalResult;

            if (result == "Win")
                resultText = "Win";
            else if (result == "Lose")
                resultText = "Lose";
            else
                resultText = "Draw";
        }

        resultMessage.text = resultText;

        yield return new WaitForSeconds(3f);

        StartCoroutine(SceneLoader.Instance.LoadSceneCoroutine("Lobby", 0.5f));
    }
}
