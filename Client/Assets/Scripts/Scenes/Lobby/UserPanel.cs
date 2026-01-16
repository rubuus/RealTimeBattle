using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/* 
 * UserPanel.cs
 * 
 * 역할 : 
 * - User UI Panel 관리
 * 
*/

public class UserPanel : MonoBehaviour
{
    public static Action<int> OnCharacterChanged;

    [SerializeField] private Image image;
    [SerializeField] private TMP_Text nicknameText;


    private void Awake()
    {
        nicknameText.text = AuthManager.Instance.Nickname;
        UpdateProfileImage(AuthManager.Instance.ProfileImage);
    }

    // Panel 활성화 시, 이벤트 추가
    private void OnEnable()
    {
        OnCharacterChanged += UpdateProfileImage;
    }

    // Panel 비활성화 시, 이벤트 제거
    private void OnDisable()
    {
        OnCharacterChanged -= UpdateProfileImage;
    }

    // DB에 저장돼있는 이미지 이름을 기준
    // UI Sprite의 이미지 변경
    private void UpdateProfileImage(int idx)
    {
        image.sprite = UserData.Instance.images[idx];
    }
}
