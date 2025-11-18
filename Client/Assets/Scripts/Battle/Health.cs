using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private HPBar hpBar;

    public int maxHp = 100;
    public int currentHp;

    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();

        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        currentHp = maxHp;
    }

    private void Start()
    {
        AssignHPBar();
        UpdateHPBar();
    }

    private void AssignHPBar()
    {
        if (hpBar != null) return;

        if (playerController.isLeftSide)
            hpBar = GameObject.Find("HP_Bar_Left").GetComponentInChildren<HPBar>();
        else
            hpBar = GameObject.Find("HP_Bar_Right").GetComponentInChildren<HPBar>();
    }


    public void TakeDamage(int amount)
    {
        currentHp -= amount;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        UpdateHPBar();

        playerController.OnHurt();

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void UpdateHPBar()
    {
        if (hpBar == null)
        {
            Debug.LogError("HPBar가 null임! 연결 안 됨!");
            return;
        }
        float rate = (float)currentHp / maxHp;
        hpBar.SetValue(rate);
    }

    private void Die()
    {
        // TODO: 죽었을 때 애니메이션 or 리스폰 처리 넣을 곳
    }
}
