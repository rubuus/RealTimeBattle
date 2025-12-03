using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileImageButton : MonoBehaviour
{
    [SerializeField] private List<Button> images = new List<Button>();
    [SerializeField] private Image profile;

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
        StartCoroutine(AuthManager.Instance.ChangeProfileImage(idx));
        UserPanel.OnCharacterChanged?.Invoke(idx);
    }
}
