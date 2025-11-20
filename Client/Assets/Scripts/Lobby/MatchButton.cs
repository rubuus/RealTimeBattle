using UnityEngine;

public class MatchButton : MonoBehaviour
{
    public void OnMatchStart()
    {
        if (!SocketClient.Instance.IsConnected)
            _ = SocketClient.Instance.Connect();

        SocketClient.Instance.Send("MATCH_START");
        Debug.Log("MATCH_START sent");
    }
}
