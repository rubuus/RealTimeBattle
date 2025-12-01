using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private HPBar hpBar;

    public int maxHp = 100;
    public int currentHp;

    private void Awake()
    {
        currentHp = 100;
    }

    private void Start()
    {
        AssignHPBar();
        UpdateHPBar();
    }

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

    public void UpdateHPBar()
    {
        if (hpBar == null) return;

        float rate = (float)currentHp / maxHp;
        hpBar.SetValue(rate);
    }

    private void Die()
    {
        // TODO: 죽었을 때 애니메이션 or 리스폰 처리 넣을 곳
    }
}
