using UnityEngine;

public class PunchHitBox : MonoBehaviour
{
    public int damage = 10;

    private PlayerController owner; // 부모 플레이어 저장

    private void Awake()
    {
        owner = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        HurtBox hurtbox = col.GetComponent<HurtBox>();

        if (hurtbox == null) return;

        // 자기 자신은 무시
        if (hurtbox.GetComponentInParent<PlayerController>() == owner) return;

        // 데미지 적용
        hurtbox.ApplyDamage(damage);
    }
}
