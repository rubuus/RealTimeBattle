using UnityEngine;

public class MatchButton : MonoBehaviour
{
    public static MatchButton Instance;
    public bool isMatching = false;

    private void Awake()
    {
        Instance = this;
    }

    public async void OnMatchStart()
    {
        if (isMatching)
        {
            Debug.Log("Already matching, ignoring...");
            return;
        }

        isMatching = true;

        if (!SocketClient.Instance.connected)
        {
            await SocketClient.Instance.Connect(); // await로 바꿔서 순서 보장
        }

        SocketClient.Instance.Send(new BasePacket { type = "MATCH_START" });

        Debug.Log("MATCH_START sent");
    }
}
