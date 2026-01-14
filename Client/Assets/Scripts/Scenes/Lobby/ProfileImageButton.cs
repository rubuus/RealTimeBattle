using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileImageButton : MonoBehaviour
{
    [SerializeField] private List<Button> images = new List<Button>();
    [SerializeField] private Image profile;
    Coroutine sendJob;

    private void Awake()
    {
        for (int i = 0; i < images.Count; i++)
        {
            int idx = i;
            images[idx].onClick.AddListener(() => ChangeImage(idx));
        }
    }

    private void ChangeImage(int idx)
    {
        UserPanel.OnCharacterChanged?.Invoke(idx);

        if (sendJob != null)
            StopCoroutine(sendJob);

        // 300ms ÈÄ Àü¼Û
        sendJob = StartCoroutine(SendProfileImageDelayed(idx));
    }

    private IEnumerator SendProfileImageDelayed(int idx)
    {
        yield return new WaitForSeconds(0.3f);
        yield return AuthManager.Instance.ChangeProfileImage(idx);
    }
}
