using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserPanel : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text nicknameText;
    public static Action<int> OnCharacterChanged;

    private void Awake()
    {
        nicknameText.text = AuthManager.Instance.Nickname;
        UpdateProfileImage(AuthManager.Instance.ProfileImage);
    }

    private void OnEnable()
    {
        OnCharacterChanged += UpdateProfileImage;
    }

    private void OnDisable()
    {
        OnCharacterChanged -= UpdateProfileImage;
    }

    private void UpdateProfileImage(int idx)
    {
        image.sprite = UserData.Instance.images[idx];
    }
}
