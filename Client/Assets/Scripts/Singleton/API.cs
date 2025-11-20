using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;

public class API : MonoBehaviour
{
    public static API Instance { get; private set; }

    [SerializeField] private string baseUrl = "http://localhost:5146";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 중복 생성 방지
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 전환에도 유지
    }

    public IEnumerator SendJsonRequest<T>(
    string endpoint,
    string method,
    T data,
    Action<string> onSuccess,
    Action<string> onError)
    {
        string json = JsonUtility.ToJson(data);
        var req = new UnityWebRequest($"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}", method);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        // ✅ JWT 토큰이 존재하면 Authorization 헤더 자동 추가
        if (!string.IsNullOrEmpty(AuthManager.Instance.AccessToken))
            req.SetRequestHeader("Authorization", "Bearer " + AuthManager.Instance.AccessToken);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke(req.error);
    }
}
