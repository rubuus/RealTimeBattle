using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;
using Newtonsoft.Json;

public class API : MonoBehaviour
{
    public static API Instance { get; private set; }

    [SerializeField] private string baseUrl = "http://localhost:5146";

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;

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
    T data = default,
    Action<string> onSuccess = null,
    Action<string> onError = null)
    {
        var url = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

        var req = new UnityWebRequest(url, method);
        req.downloadHandler = new DownloadHandlerBuffer();

        // data가 있을 때만 body 추가 (GET 대응)
        if (data != null && !method.Equals(UnityWebRequest.kHttpVerbGET))
        {
            var json = JsonConvert.SerializeObject(data);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.SetRequestHeader("Content-Type", "application/json");
        }

        // JWT 자동 첨부
        var token = AuthManager.Instance.AccessToken;
        if (!string.IsNullOrEmpty(token))
            req.SetRequestHeader("Authorization", $"Bearer {token}");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke(req.error);
    }
}
