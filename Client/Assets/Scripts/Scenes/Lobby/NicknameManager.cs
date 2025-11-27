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
        }
        else if (nicknameField.text.Length > 9)
        {
            ShowError("8 Character Limit");
        }
    }

    private void ShowError(string message)
    {
        errorMessage.text = message;
        errorMessage.gameObject.SetActive(true);
        guideMessage.gameObject.SetActive(false);
    }
}
