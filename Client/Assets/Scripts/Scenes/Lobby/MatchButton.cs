using UnityEngine;

/* 
 * MatchButton.cs
 * 
 * 역할 : 
 * - Match 버튼 클릭 시, 매칭 시작 상태를 소켓 서버에 알림
 * 
*/

public class MatchButton : MonoBehaviour
{
    public static MatchButton Instance;

    public bool isMatching = false;

    private void Awake()
    {
        Instance = this;
    }

    public void OnMatchStart()
    {
        if (isMatching)
        {
            Debug.Log("Already matching, ignoring...");
            return;
        }

        isMatching = true;

        if (SocketClient.Instance.useCppServer)
            _ = SocketClient.Instance.CppSendHeaderOnly(C2S_HeaderType.MATCH_START);
        else
            _ = SocketClient.Instance.CsharpSend(new BasePacket { type = "MATCH_START" });

        Debug.Log("MATCH_START sent");
    }
}
