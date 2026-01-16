using UnityEngine;

/*
 * Health.cs
 * 
 * 역할 :
 * - 체력 UI 업데이트
 * 
 */
public class Health : MonoBehaviour
{
    public int maxHp = 100;
    public int currentHp;

    [SerializeField] private HPBar hpBar;

    private void Awake()
    {
        currentHp = maxHp;
    }

    private void Start()
    {
        AssignHPBar();
        UpdateHPBar();
    }

    // HP 업데이트 할 유저의 체력바 오브젝트 찾기
    private void AssignHPBar()
    {
        if (hpBar != null) return;

        if (CompareTag("Player"))
        {
            if (SocketClient.Instance.side == "LEFT")
                hpBar = GameObject.Find("HP_Bar_Left").GetComponentInChildren<HPBar>();
            else
                hpBar = GameObject.Find("HP_Bar_Right").GetComponentInChildren<HPBar>();
        }
        else
        {
            if (SocketClient.Instance.side == "LEFT")
                hpBar = GameObject.Find("HP_Bar_Right").GetComponentInChildren<HPBar>();
            else
                hpBar = GameObject.Find("HP_Bar_Left").GetComponentInChildren<HPBar>();
        }
    }

    // 변경된 HP를 UI 표시
    public void UpdateHPBar()
    {
        if (hpBar == null) return;

        float rate = (float)currentHp / maxHp;
        hpBar.SetValue(rate);
    }
}
