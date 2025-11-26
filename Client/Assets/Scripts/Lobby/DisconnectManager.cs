using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisconnectManager : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;   // 1프레임 대기

        SocketClient.Instance.Disconnect();
    }
}
