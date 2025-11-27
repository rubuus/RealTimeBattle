using TMPro;
using UnityEngine;

public class UserPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;

    private void Awake()
    {
        nicknameText.text = AuthManager.Instance.Nickname;
    }
}
