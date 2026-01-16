using UnityEngine.SceneManagement;
using UnityEngine;

/*
 * BootLoader.cs
 * 
 * 역할 :
 * - Boot Scene에서 클라 데이터 불러온 후, 로그인 씬 이동
 * 
 */

public class BootLoader : MonoBehaviour
{
    private void Start()
    {
        SettingsData.Load();
        SceneManager.LoadScene("Login");
    }
}