using Newtonsoft.Json;
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
    public int ProfileImage { get; private set; }

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
        var req = new AccountCheckRequest { AccountId = accountId };

        yield return API.Instance.SendJsonRequest(
            "users/check-account", "POST", req,
            onSuccess: res => {
                var parsed = JsonConvert.DeserializeObject<DuplicateCheckResponse>(res);
                onDone?.Invoke(parsed.IsDuplicate, parsed.Message);
            },
            onError: err => {
                onDone?.Invoke(true, "Request Failed");
            }
        );
    }

    public IEnumerator CheckDuplicateNickname(string nickname, System.Action<bool, string> onDone)
    {
        var req = new NicknameCheckRequest { Nickname = nickname };

        yield return API.Instance.SendJsonRequest(
            "users/check-nickname", "POST", req,
            onSuccess: res => {
                var parsed = JsonConvert.DeserializeObject<DuplicateCheckResponse>(res);
                onDone?.Invoke(parsed.IsDuplicate, parsed.Message);
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
            AccountId = accountId,
            Password = password,
            Nickname = nickname
        };

        yield return API.Instance.SendJsonRequest(
            "users/register",
            "POST",
            signUpData,
            onSuccess: res =>
            {
                var response = JsonConvert.DeserializeObject<RegisterResponse>(res);
                Debug.Log($"회원가입 성공: {response.Message} (UserId: {response.UserId})");
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
            AccountId = accountId,
            Password = password
        };

        yield return API.Instance.SendJsonRequest(
            "users/login", "POST", loginData,
            onSuccess: res =>
            {
                var response = JsonConvert.DeserializeObject<LoginResponse>(res);

                if (!string.IsNullOrEmpty(response.AccessToken))
                {
                    UserId = response.UserId;
                    Nickname = response.Nickname;
                    AccessToken = response.AccessToken;
                    ProfileImage = response.ProfileImage;

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

    public IEnumerator ChangeNicknameRequest(string nickname)
    {
        var req = new ChangeNicknameRequest
        {
            Nickname = nickname,
            AccessToken = AccessToken
        };

        yield return API.Instance.SendJsonRequest(
            "users/change-nickname", "POST", req,
            onSuccess: res => {
                Application.Quit();
            },
            onError: err => {
                Debug.Log(err);
            }
        );
    }

    public IEnumerator DeleteAccount()
    {
        var req = new DeleteAccountRequest
        {
            AccessToken = AccessToken
        };

        yield return API.Instance.SendJsonRequest(
            "users/delete-account", "POST", req,
            onSuccess: res => {
                Application.Quit();
            },
            onError: err => {
                Debug.Log(err);
            }
        );
    }

    public IEnumerator ChangeProfileImage(int idx)
    {
        var req = new ChangeProfileImageRequest
        {
            ProfileImage = idx,
            AccessToken = AccessToken
        };

        yield return API.Instance.SendJsonRequest(
            "users/profile-image", "POST", req,
            onSuccess: res => {
                ProfileImage = req.ProfileImage;
            },
            onError: err => {
                Debug.Log(err);
            }
        );
    }
}
