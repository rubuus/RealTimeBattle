using System.Collections;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchManager : MonoBehaviour
{
    [SerializeField] TMP_Text leftText;
    [SerializeField] TMP_Text rightText;
    [SerializeField] GameObject leftPlayer;
    [SerializeField] GameObject rightPlayer;


    private void Start()
    {
        StartCoroutine(UIActive());
        StartCoroutine(LoadBattle());
    }

    private IEnumerator UIActive()
    {
        string side = SocketClient.Instance.side;

        int myId = SocketClient.Instance.myUserId;
        int enemyId = SocketClient.Instance.enemyUserId;

        TMP_Text myText = (side == "LEFT") ? leftText : rightText;
        TMP_Text enemyText = (side == "LEFT") ? rightText : leftText;

        GameObject myPlayer = (side == "LEFT") ? leftPlayer : rightPlayer;
        GameObject enemyPlayer = (side == "LEFT") ? rightPlayer : leftPlayer;
        enemyPlayer.GetComponent<SpriteRenderer>().color = new Color(0f, 1f, 1f);

        yield return API.Instance.SendJsonRequest<object>(
            endpoint: $"users/{myId}",
            method: "GET",
            data: null,
            onSuccess: res => {
                var user = JsonUtility.FromJson<NicknameResponse>(res);
                myText.text = user.nickname;
            },
            onError: err => Debug.LogError(err)
        );

        yield return API.Instance.SendJsonRequest<object>(
            endpoint: $"users/{enemyId}",
            method: "GET",
            data: null,
            onSuccess: res => {
                var user = JsonUtility.FromJson<NicknameResponse>(res);
                enemyText.text = user.nickname;
            },
            onError: err => Debug.LogError(err)
        );
    }

    private IEnumerator LoadBattle()
    {
        yield return new WaitForSeconds(5f);

        SceneManager.LoadScene("Battle");
    }
}
