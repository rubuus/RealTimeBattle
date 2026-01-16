using TMPro;
using UnityEngine;
using UnityEngine.UI;

/* 
 * NicknameManager.cs
 * 
 * 역할 : 
 * - Nickname 변경 로직 관리
 * 
*/

public class NicknameManager : MonoBehaviour
{
    public TMP_Text guideMessage;
    public TMP_Text errorMessage;
    public TMP_InputField nicknameField;
    public Button changeNicknameButton;

    void Awake()
    {
        changeNicknameButton.onClick.AddListener(ChangeNickname);
    }

    // 중복 체크 후, 변경된 Nickname 서버에 저장
    private void ChangeNickname()
    {
        string nickname = nicknameField.text;

        if (string.IsNullOrEmpty(nickname))
        {
            ShowError("Check Field");
            return;
        }
        else if (nicknameField.text.Length > 9)
        {
            ShowError("8 Character Limit");
            return;
        }

        StartCoroutine(AuthManager.Instance.CheckDuplicateNickname(nickname, (isDuplicate, message) =>
        {
            if (isDuplicate)
            {
                ShowError(message);
                return;
            }

        }));

        StartCoroutine(AuthManager.Instance.ChangeNicknameRequest(nickname));
    }

    // UI에 Error 띄우기
    private void ShowError(string message)
    {
        errorMessage.text = message;
        errorMessage.gameObject.SetActive(true);
        guideMessage.gameObject.SetActive(false);
    }
}
