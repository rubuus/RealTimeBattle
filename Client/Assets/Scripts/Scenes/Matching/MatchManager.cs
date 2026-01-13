using Newtonsoft.Json;
using System.Collections;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MatchManager : MonoBehaviour
{
    [SerializeField] Image leftImage;
    [SerializeField] Image rightImage;
    [SerializeField] TMP_Text leftText;
    [SerializeField] TMP_Text rightText;
    [SerializeField] GameObject leftPlayer;
    [SerializeField] GameObject rightPlayer;

    private void Start()
    {
        UIActive();
        StartCoroutine(SceneLoader.Instance.LoadBattle());
    }

    private void UIActive()
    {
        bool left = SocketClient.Instance.side == "LEFT";

        Image myImage = left ? leftImage : rightImage;
        Image enemyImage = left ? rightImage : leftImage;

        TMP_Text myText = left ? leftText : rightText;
        TMP_Text enemyText = left ? rightText : leftText;

        GameObject enemyPlayer = left ? rightPlayer : leftPlayer;
        enemyPlayer.GetComponent<SpriteRenderer>().color = new Color(0f, 1f, 1f);

        StartCoroutine(GetMyProfile(myImage, myText));
        StartCoroutine(GetEnemyProfile(enemyImage, enemyText));

        if (SocketClient.Instance.useCppServer)
            _ = SocketClient.Instance.SendHeaderOnlyAsync(C2S_PacketType.BATTLE_READY);
        else
            _ = SocketClient.Instance.Send(new BasePacket { type = "BATTLE_READY" });
    }

    private IEnumerator GetMyProfile(Image image, TMP_Text text)
    {
        yield return API.Instance.SendJsonRequest<object>(
            endpoint: $"users/{SocketClient.Instance.myUserId}",
            method: "GET",
            data: null,
            onSuccess: res => {
                var user = JsonConvert.DeserializeObject<ProfileResponse>(res);
                text.text = user.Nickname;
                image.sprite = UserData.Instance.images[user.ProfileImage];
            },
            onError: err => Debug.LogError(err)
        );
    }

    private IEnumerator GetEnemyProfile(Image image, TMP_Text text)
    {
        yield return API.Instance.SendJsonRequest<object>(
            endpoint: $"users/{SocketClient.Instance.enemyUserId}",
            method: "GET",
            data: null,
            onSuccess: res => {
                var user = JsonConvert.DeserializeObject<ProfileResponse>(res);
                text.text = user.Nickname;
                image.sprite = UserData.Instance.images[user.ProfileImage];
            },
            onError: err => Debug.LogError(err)
        );
    }
}
