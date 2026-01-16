using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/* 
 * ProfileImageButton.cs
 * 
 * 역할 : 
 * - ProfileImage 변경 시, API 요청
 * 
*/

public class ProfileImageButton : MonoBehaviour
{
    [SerializeField] private List<Button> images = new List<Button>();
    [SerializeField] private Image profile;

    private Coroutine sendJob;

    private void Awake()
    {
        for (int i = 0; i < images.Count; i++)
        {
            int idx = i;
            images[idx].onClick.AddListener(() => ChangeImage(idx));
        }
    }

    // 바꾼 Image를 API 서버에 등록
    private void ChangeImage(int idx)
    {
        UserPanel.OnCharacterChanged?.Invoke(idx);

        if (sendJob != null)
            StopCoroutine(sendJob);

        // 0.3초 후, API 요청
        sendJob = StartCoroutine(SendProfileImageDelayed(idx));
    }

    private IEnumerator SendProfileImageDelayed(int idx)
    {
        yield return new WaitForSeconds(0.3f);
        yield return AuthManager.Instance.ChangeProfileImage(idx);
    }
}
