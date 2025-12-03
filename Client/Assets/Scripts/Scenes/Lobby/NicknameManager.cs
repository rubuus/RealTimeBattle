using System.Collections;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameManager : MonoBehaviour
{
    public TMP_Text guideMessage;
    public TMP_Text errorMessage;
    public TMP_InputField nicknameField;
    [SerializeField] private Button changeNicknameButton;

    void Awake()
    {
        changeNicknameButton.onClick.AddListener(ChangeNickname);
    }

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

    private void ShowError(string message)
    {
        errorMessage.text = message;
        errorMessage.gameObject.SetActive(true);
        guideMessage.gameObject.SetActive(false);
    }
}
