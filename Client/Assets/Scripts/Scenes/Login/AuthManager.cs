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
    public int ProfileImage { get; private set; }
    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }

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
                try
                {
                    var response = JsonConvert.DeserializeObject<RegisterResponse>(res);
                    Debug.Log($"회원가입 성공: {response.Message} (UserId: {response.UserId})");
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            },
            onError: err =>
            {
                Debug.LogWarning($"회원가입 실패: {err}");
            }
        );
    }

    // 계정 아이디 중복 확인
    public IEnumerator CheckDuplicateAccount(string accountId, Action<bool, string> onDone)
    {
        bool finished = false;

        // 중복 방지 및 완료 신호 보냄
        void Finish(bool isDuplicate, string msg)
        {
            if (finished) return;
            finished = true;
            onDone?.Invoke(isDuplicate, msg);
        }

        yield return API.Instance.SendJsonRequest(
            endpoint: "users/check-account", 
            method: UnityWebRequest.kHttpVerbPOST, 
            data: new AccountCheckRequest { AccountId = accountId },
            onSuccess: res => {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<DuplicateCheckResponse>(res);
                    Finish(parsed.IsDuplicate, parsed.Message);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    Finish(true, "Parse Error");
                }
            },
            onError: err => {
                Debug.Log(err);
                Finish(false, "Request Failed");
            }
        );
    }

    // 로그인 요청
    public IEnumerator Login(string accountId, string password, Action<bool> onResult)
    {
        bool finished = false;

        // 중복 방지 및 완료 신호 보냄
        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            onResult?.Invoke(success);
        }

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
                    RefreshToken = response.RefreshToken;

                    Finish(true);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    Finish(true);
                }
            },
            onError: err => 
            { 
                Debug.LogError(err);
                Finish(false);
            }
        );
    }

    // Nickname 중복 확인
    public IEnumerator CheckDuplicateNickname(string nickname, System.Action<bool, string> onDone)
    {
        bool finished = false;

        // 중복 방지 및 완료 신호 보냄
        void Finish(bool isDuplicate, string msg)
        {
            if (finished) return;
            finished = true;
            onDone?.Invoke(isDuplicate, msg);
        }

        yield return API.Instance.SendJsonRequest(
            endpoint: "users/check-nickname",
            method: UnityWebRequest.kHttpVerbPOST,
            data: new NicknameCheckRequest { Nickname = nickname },
            onSuccess: res => {
                try
                {
                    var parsed =
                        JsonConvert.DeserializeObject<DuplicateCheckResponse>(res);

                    Finish(parsed.IsDuplicate, parsed.Message);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    Finish(true, "Parse Error");
                }
            },
            onError: err => {
                Debug.LogError(err);
                Finish(false, "Request Failed");
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
    public IEnumerator DeleteAccount(string pw)
    {
        yield return API.Instance.SendJsonRequest(
            endpoint: "users/delete-account",
            method: UnityWebRequest.kHttpVerbPOST,
            data: new DeleteAccountRequest { Password = pw },
            onSuccess: res => {
                Application.Quit();
            },
            onError: Debug.Log
        );
    }

    // 재발급 토큰 -> 인증 토큰
    public IEnumerator RefreshTokenRequest(Action<bool> onResult)
    {
        var req = new UnityWebRequest(
            $"{API.Instance.baseUrl}/auth/refresh",
            UnityWebRequest.kHttpVerbPOST
        );

        req.downloadHandler = new DownloadHandlerBuffer();

        // RefreshToken 전달
        req.SetRequestHeader(
            "Authorization",
            "Bearer " + RefreshToken
        );

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var res = JsonConvert.DeserializeObject<RefreshTokenResponse>(
                    req.downloadHandler.text
                );

                AccessToken = res.AccessToken;
                onResult?.Invoke(true);
            }
            catch
            {
                onResult?.Invoke(false);
            }
        }
        else
        {
            onResult?.Invoke(false);
        }
    }

    public IEnumerator Logout()
    {
        Application.Quit();
        yield break;
    }
}
