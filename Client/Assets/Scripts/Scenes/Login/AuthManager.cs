using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    public int UserId { get; private set; }
    public string Nickname { get; private set; }
    public string AccessToken { get; private set; }

    [System.Serializable]
    public class RegisterRequest
    {
        public string accountId;
        public string password;
        public string nickname;
    }

    [System.Serializable]
    public class RegisterResponse
    {
        public int userId;
        public string message;
    }

    [System.Serializable]
    public class LoginRequest
    {
        public string accountId;
        public string password;
    }

    [System.Serializable]
    public class LoginResponse
    {
        public int userId;
        public string nickname;
        public string accessToken;
    }

    [System.Serializable]
    public class DuplicateCheckResponse
    {
        public bool isDuplicate;
        public string message;
    }

    [System.Serializable]
    public class AccountCheckRequest
    {
        public string accountId;
    }

    [System.Serializable]
    public class NicknameCheckRequest
    {
        public string nickname;
    }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public IEnumerator CheckDuplicateAccount(string accountId, System.Action<bool, string> onDone)
    {
        var req = new AccountCheckRequest { accountId = accountId };

        yield return API.Instance.SendJsonRequest(
            "users/check-account", "POST", req,
            onSuccess: res => {
                var parsed = JsonUtility.FromJson<DuplicateCheckResponse>(res);
                onDone?.Invoke(parsed.isDuplicate, parsed.message);
            },
            onError: err => {
                onDone?.Invoke(true, "Request Failed");
            }
        );
    }

    public IEnumerator CheckDuplicateNickname(string nickname, System.Action<bool, string> onDone)
    {
        var req = new NicknameCheckRequest { nickname = nickname };

        yield return API.Instance.SendJsonRequest(
            "users/check-nickname", "POST", req,
            onSuccess: res => {
                var parsed = JsonUtility.FromJson<DuplicateCheckResponse>(res);
                onDone?.Invoke(parsed.isDuplicate, parsed.message);
            },
            onError: err => {
                onDone?.Invoke(true, "Request Failed"); // 실패 시 보수적으로 막기
            }
        );
    }

    // 회원가입 요청
    public IEnumerator SignUp(string accountId, string password, string nickname)
    {
        var signUpData = new RegisterRequest
        {
            accountId = accountId,
            password = password,
            nickname = nickname
        };

        yield return API.Instance.SendJsonRequest(
            "users/register",
            "POST",
            signUpData,
            onSuccess: res =>
            {
                var response = JsonUtility.FromJson<RegisterResponse>(res);
                Debug.Log($"회원가입 성공: {response.message} (UserId: {response.userId})");
            },
            onError: err =>
            {
                Debug.LogWarning($"회원가입 실패: {err}");
            }
        );
    }

    // 로그인 요청
    public IEnumerator Login(string accountId, string password, Action<bool> onResult)
    {
        var loginData = new LoginRequest
        {
            accountId = accountId,
            password = password
        };

        yield return API.Instance.SendJsonRequest(
            "users/login", "POST", loginData,
            onSuccess: res =>
            {
                var response = JsonUtility.FromJson<LoginResponse>(res);

                if (!string.IsNullOrEmpty(response.accessToken))
                {
                    UserId = response.userId;
                    Nickname = response.nickname;
                    AccessToken = response.accessToken;

                    onResult?.Invoke(true); // 성공
                }
                else
                {
                    onResult?.Invoke(false); // 실패
                }
            },
            onError: err =>
            {
                onResult?.Invoke(false);
            }
        );
    }

}
