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
        StartCoroutine(SceneLoader.Instance.LoadBattle());
    }

    private IEnumerator UIActive()
    {
        bool left = SocketClient.Instance.side == "LEFT";
        int myId = SocketClient.Instance.myId;
        int enemyId = SocketClient.Instance.enemyId;

        TMP_Text myText = left ? leftText : rightText;
        TMP_Text enemyText = left ? rightText : leftText;

        GameObject myPlayer = left ? leftPlayer : rightPlayer;
        GameObject enemyPlayer = left ? rightPlayer : leftPlayer;
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

        SocketClient.Instance.Send(new BasePacket { type = "BATTLE_READY" });
    }
}
