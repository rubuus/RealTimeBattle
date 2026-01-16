using Newtonsoft.Json;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/*
 * MatchManager.cs
 * 
 * 역할 :
 * - Matching Scene UI 관리
 * 
 */

public class MatchManager : MonoBehaviour
{
    [SerializeField] private Image leftImage;
    [SerializeField] private Image rightImage;
    [SerializeField] private TMP_Text leftText;
    [SerializeField] private TMP_Text rightText;
    [SerializeField] private GameObject leftPlayer;
    [SerializeField] private GameObject rightPlayer;

    private void Start()
    {
        UIActive();
        StartCoroutine(SceneLoader.Instance.LoadBattle());
    }

    // side 값에 따라 UI 표시
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
            _ = SocketClient.Instance.CppSendHeaderOnly(C2S_HeaderType.BATTLE_READY);
        else
            _ = SocketClient.Instance.CsharpSend(new BasePacket { type = "BATTLE_READY" });
    }

    // 내 프로필 불러오기
    private IEnumerator GetMyProfile(Image image, TMP_Text text)
    {
        yield return API.Instance.SendJsonRequest<object>(
            endpoint: $"users/{SocketClient.Instance.myUserId}",
            method: UnityWebRequest.kHttpVerbGET,
            data: null,
            onSuccess: res => {
                var user = JsonConvert.DeserializeObject<ProfileResponse>(res);
                text.text = user.Nickname;
                image.sprite = UserData.Instance.images[user.ProfileImage];
            },
            onError: err => Debug.LogError(err)
        );
    }

    // 상대 프로필 불러오기
    private IEnumerator GetEnemyProfile(Image image, TMP_Text text)
    {
        yield return API.Instance.SendJsonRequest<object>(
            endpoint: $"users/{SocketClient.Instance.enemyUserId}",
            method: UnityWebRequest.kHttpVerbGET,
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
