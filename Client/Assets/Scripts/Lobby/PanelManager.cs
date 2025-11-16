using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PanelType
{
    Record,
    Setting,
    Sound,
    Language,
    Account,
    ChangeNickname,
    DeleteAccount,
    Match
}

[System.Serializable]
public struct PanelItem
{
    public PanelType type;      // 어떤 패널인지
    public GameObject panel;    // 실제 패널 오브젝트
    public Button openButton; 
    public Button closeButton;
}

public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance { get; private set; }

    [SerializeField] private List<PanelItem> panelItems;

    private Dictionary<PanelType, GameObject> panelDict = new();


    [SerializeField] private NicknameManager nicknameManager;
    [SerializeField] private Button deleteAccountButton;

    private void Awake()
    {
        // 딕셔너리 초기화
        foreach (var item in panelItems)
        {
            if (item.panel == null) continue;

            panelDict[item.type] = item.panel;
            item.panel.SetActive(false); // 시작 시 전부 끄기

            if (item.openButton != null)
            {
                PanelType capturedType = item.type; // 클로저 버그(loop 중 참조하면 마지막 값 참조) 방지
                item.openButton.onClick.AddListener(() => ShowPanel(capturedType));
            }

            if (item.closeButton != null)
            {
                PanelType capturedType = item.type; // 클로저 버그(loop 중 참조하면 마지막 값 참조) 방지
                item.closeButton.onClick.AddListener(() => ClosePanel(capturedType));
            }
        }

        deleteAccountButton.onClick.AddListener(DeleteAccount);
    }

    public void ShowPanel(PanelType type)
    {
        if (panelDict.TryGetValue(type, out GameObject target))
        {
            PanelType[] group = { PanelType.Sound, PanelType.Language, PanelType.Account };

            if (type == PanelType.Setting)
            {
                // SettingPanel 먼저 켜기
                target.SetActive(true);

                // 그룹에 속한 패널 모두 끄기
                foreach (var g in group)
                {
                    if (panelDict.TryGetValue(g, out GameObject p))
                        p.SetActive(false);
                }

                // SoundPanel만 기본으로 켜기
                if (panelDict.TryGetValue(PanelType.Sound, out GameObject soundPanel))
                    soundPanel.SetActive(true);

                return;
            }

            // 만약 클릭한 게 group 안에 포함되어 있다면
            if (System.Array.Exists(group, g => g == type))
            {
                // group 안에 속한 패널들은 전부 끄기
                foreach (var g in group)
                {
                    if (panelDict.TryGetValue(g, out GameObject p))
                        p.SetActive(false);
                }
            }

            if (type == PanelType.ChangeNickname)
            {
                nicknameManager.guideMessage.gameObject.SetActive(true);
            }

            target.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"패널 '{type}' 이(가) 등록되지 않았음");
        }
    }

    public void ClosePanel(PanelType type)
    {
        if (panelDict.TryGetValue(type, out GameObject target))
        {
            if (type == PanelType.ChangeNickname)
            {
                nicknameManager.errorMessage.gameObject.SetActive(false);
                nicknameManager.nicknameField.text = string.Empty;
            }

            target.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"패널 '{type}' 이(가) 등록되지 않았음");
        }
    }

    private void DeleteAccount()
    {
        Application.Quit();
    }
}
