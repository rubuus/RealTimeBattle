using UnityEngine;

public class HurtBox : MonoBehaviour
{
    private Health health;

    private void Awake()
    {
        health = GetComponentInParent<Health>();
    }

    public void ApplyDamage(int amount)
    {
        health.TakeDamage(amount);
    }
}
