using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisconnectManager : MonoBehaviour
{
    private IEnumerator Start()
    {
        if (SceneLoader.Instance.lastSceneName != "Result")
            yield break;

        yield return null;

        SocketClient.Instance.Disconnect();
    }
}
