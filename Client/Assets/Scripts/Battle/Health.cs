using UnityEngine;

public class Health : MonoBehaviour
{
    private PlayerController playerController;

    [SerializeField] private HPBar hpBar;

    public int maxHp = 100;
    public int currentHp;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();

        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
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
        float rate = (float)currentHp / maxHp;
        hpBar.SetValue(rate);
    }

    private void Die()
    {
        // TODO: 죽었을 때 애니메이션 or 리스폰 처리 넣을 곳
    }
}
