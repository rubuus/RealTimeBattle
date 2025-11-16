using UnityEngine;

public class UserData : MonoBehaviour
{
    public static UserData Instance { get; private set; }

    public int UserId { get; private set; }
    public string Nickname { get; private set; }
    public string Token { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveUserInfo(int userId, string nickname, string token = "")
    {
        UserId = userId;
        Nickname = nickname;
        Token = token;
    }

    public void Clear()
    {
        UserId = 0;
        Nickname = string.Empty;
        Token = string.Empty;
    }
}
