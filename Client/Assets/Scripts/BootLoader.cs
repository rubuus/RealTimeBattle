using UnityEngine.SceneManagement;
using UnityEngine;

public class BootLoader : MonoBehaviour
{
    private void Awake()
    {
        SettingsData.Load();
        SceneManager.LoadScene("Login");
    }
}