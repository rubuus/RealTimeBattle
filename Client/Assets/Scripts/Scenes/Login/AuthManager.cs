using Newtonsoft.Json;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/*
 * AuthManager.cs
 * 
 * 역할 :
 * - 계정 관련 API 통신 관리
 * 
 */

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
            endpoint: "users/register",
            method: UnityWebRequest.kHttpVerbPOST,
            data: signUpData,
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

    // 계정 아이디 중복 확인
    public IEnumerator CheckDuplicateAccount(string accountId, System.Action<bool, string> onDone)
    {
        yield return API.Instance.SendJsonRequest(
            endpoint: "users/check-account", 
            method: UnityWebRequest.kHttpVerbPOST, 
            data: new AccountCheckRequest { AccountId = accountId },
            onSuccess: res => {
                var parsed = JsonConvert.DeserializeObject<DuplicateCheckResponse>(res);
                onDone?.Invoke(parsed.IsDuplicate, parsed.Message);
            },
            onError: err => {
                onDone?.Invoke(true, "Request Failed");
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
            endpoint: "users/login", 
            method: UnityWebRequest.kHttpVerbPOST, 
            data: loginData,
            onSuccess: res =>
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<LoginResponse>(res);

                    UserId = response.UserId;
                    Nickname = response.Nickname;
                    ProfileImage = response.ProfileImage;
                    AccessToken = response.Accesstoken;

                    onResult?.Invoke(true);
                }
                catch
                {
                    onResult?.Invoke(false);
                }
            },
            onError: err => { onResult?.Invoke(false); }
        );
    }

    // Nickname 중복 확인
    public IEnumerator CheckDuplicateNickname(string nickname, System.Action<bool, string> onDone)
    {
        yield return API.Instance.SendJsonRequest(
            endpoint: "users/check-nickname",
            method: UnityWebRequest.kHttpVerbPOST,
            data: new NicknameCheckRequest { Nickname = nickname },
            onSuccess: res => {
                var parsed = JsonConvert.DeserializeObject<DuplicateCheckResponse>(res);
                onDone?.Invoke(parsed.IsDuplicate, parsed.Message);
            },
            onError: err => {
                onDone?.Invoke(true, "Request Failed");
            }
        );
    }

    // Nickname 변경
    public IEnumerator ChangeNicknameRequest(string nickname)
    {
        yield return API.Instance.SendJsonRequest(
            endpoint: "users/change-nickname",
            method: UnityWebRequest.kHttpVerbPOST,
            data: new ChangeNicknameRequest { Nickname = nickname },
            onSuccess: res => {
                Application.Quit();
            },
            onError: Debug.Log
        );
    }

    // ProfileImage 변경
    public IEnumerator ChangeProfileImage(int idx)
    {
        yield return API.Instance.SendJsonRequest(
            endpoint: "users/profile-image", 
            method: UnityWebRequest.kHttpVerbPOST,
            data: new ChangeProfileImageRequest { ProfileImage = idx },
            onError: Debug.Log
        );
    }

    // 계정 삭제
    public IEnumerator DeleteAccount()
    {
        yield return API.Instance.SendJsonRequest(
            endpoint: "users/delete-account",
            method: UnityWebRequest.kHttpVerbPOST,
            data: new DeleteAccountRequest { AccessToken = AccessToken },
            onSuccess: res => {
                Application.Quit();
            },
            onError: Debug.Log
        );
    }
}
