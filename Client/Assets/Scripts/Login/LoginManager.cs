using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginManager : MonoBehaviour
{
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject signUpPanel;
    [SerializeField] private TMP_InputField idField;
    [SerializeField] private TMP_InputField pwField;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button goSignUpButton;
    [SerializeField] private TMP_Text guideMessage;
    [SerializeField] private TMP_Text errorMessage;

    public bool loadScenePossible = false;

    private void Start()
    {
        loginButton.onClick.AddListener(OnLoginClick);
        goSignUpButton.onClick.AddListener(ShowSignUp);
    }

    private void OnLoginClick()
    {
        string id = idField.text;
        string pw = pwField.text;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            ShowError("Check Field");
        }
        else
        {
            StartCoroutine(AuthManager.Instance.Login(id, pw, success =>
            {
                if (success)
                {
                    Debug.Log("로그인 성공");
                    loginPanel.gameObject.SetActive(false);
                    loadScenePossible = true;
                }
                else
                {
                    ShowError("Login Failed");
                }
            }));
        }
    }

    private void ShowError(string message)
    {
        errorMessage.text = message;
        idField.text = string.Empty;
        pwField.text = string.Empty;
        errorMessage.gameObject.SetActive(true);
        guideMessage.gameObject.SetActive(false);
    }

    private void ShowSignUp()
    {
        idField.text = string.Empty;
        pwField.text = string.Empty;
        errorMessage.gameObject.SetActive(false);
        guideMessage.gameObject.SetActive(true);
        signUpPanel.gameObject.SetActive(true);
        loginPanel.gameObject.SetActive(true);
    }
}
