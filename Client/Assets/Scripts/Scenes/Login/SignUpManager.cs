using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * SignUpManager.cs
 * 
 * 역할 :
 * - 회원가입 UI 관리
 * - 회원가입 API 요청 처리
 * 
 */

public class SignUpManager : MonoBehaviour
{
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject signUpPanel;
    [SerializeField] private GameObject nicknamePanel;
    [SerializeField] private TMP_InputField idField;
    [SerializeField] private TMP_InputField pwField;
    [SerializeField] private TMP_InputField nicknameField;
    [SerializeField] private Button signUpButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button nicknameButton;
    [SerializeField] private TMP_Text guideMessage;
    [SerializeField] private TMP_Text errorMessage;
    [SerializeField] private TMP_Text nicknameGuideMessage;
    [SerializeField] private TMP_Text nicknameErrorMessage;

    private string currentId;
    private string currentPw;

    void Start()
    {
        signUpButton.onClick.AddListener(OnSignUpClick);
        nicknameButton.onClick.AddListener(OnNicknameClick);
        cancelButton.onClick.AddListener(ShowLogin);
    }

    // SignUp 버튼 클릭 시, 아이디 중복 확인 후, Nickname 설정
    // 비밀번호는 Hash + Salt로 변환되기 때문에 중복 체크 필요 X
    private void OnSignUpClick()
    {
        currentId = string.Empty;
        currentPw = string.Empty;

        string id = idField.text;
        string pw = pwField.text;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            ShowError("Check Field");
            return;
        }
        else if (id.Length < 5 || pw.Length < 5)
        {
            ShowError("More Than 5 Letters");
            return;
        }
        else
        {
            currentId = id;
            currentPw = pw;

            StartCoroutine(AuthManager.Instance.CheckDuplicateAccount(id, (isDuplicate, message) =>
            {
                if (isDuplicate)
                {
                    ShowError(message);
                    return;
                }

                ShowNickname();
            }));
            
        }
    }

    // Nickname 설정까지 완료 되면, API 요청(계정 생성)
    private void OnNicknameClick()
    {
        string nickname = nicknameField.text;

        if (string.IsNullOrEmpty(nickname))
        {
            ShowError("Check Field");
            return;
        }
        else if (nickname.Length > 8)
        {
            ShowError("Limit 8 Letters");
        }
        else
        {
            StartCoroutine(AuthManager.Instance.CheckDuplicateNickname(nickname, (isDuplicate, message) =>
            {
                if (isDuplicate)
                {
                    ShowError(message);
                    return;
                }
            }));

            StartCoroutine(AuthManager.Instance.SignUp(currentId, currentPw, nickname));
            ShowLogin();
        }
    }

    // UI에 Error 띄우기
    private void ShowError(string message)
    {
        if (signUpPanel.gameObject.activeSelf)
        {
            errorMessage.text = message;
            idField.text = string.Empty;
            pwField.text = string.Empty;
            errorMessage.gameObject.SetActive(true);
            guideMessage.gameObject.SetActive(false);
        }
        else if (nicknamePanel.gameObject.activeSelf)
        {
            nicknameErrorMessage.text = message;
            nicknameField.text = string.Empty;
            nicknameErrorMessage.gameObject.SetActive(true);
            nicknameGuideMessage.gameObject.SetActive(false);
        }
        else return;
    }

    // 회원가입 성공 or 뒤로 가면, Login UI 활성화
    private void ShowLogin()
    {
        idField.text = string.Empty;
        pwField.text = string.Empty;
        guideMessage.gameObject.SetActive(true);
        errorMessage.gameObject.SetActive(false);
        nicknameGuideMessage.gameObject.SetActive(true);
        nicknameErrorMessage.gameObject.SetActive(false);
        loginPanel.gameObject.SetActive(true);
        signUpPanel.gameObject.SetActive(false);
        nicknamePanel.gameObject.SetActive(false);
    }

    private void ShowNickname()
    {
        nicknameField.text = string.Empty;
        guideMessage.gameObject.SetActive(true);
        errorMessage.gameObject.SetActive(false);
        signUpPanel.gameObject.SetActive(false);
        nicknamePanel.gameObject.SetActive(true);
    }
}
