using UnityEngine;

public class PunchHitBox : MonoBehaviour
{
    public int damage = 10;
    private bool hasHit = false;

    private void OnTriggerEnter2D(Collider2D col)
    {
        HurtBox hurtbox = col.GetComponent<HurtBox>();
        HurtBox myHurtbox = GetComponentInParent<HurtBox>();

        if (hurtbox == null || hurtbox == myHurtbox) return;

        if (hasHit) return;
        hasHit = true;

        SocketClient.Instance.Send(new HitPacket()
        {
            type = "HIT",
            hitId = SocketClient.Instance.myUserId,
            hurtId = hurtbox.PlayerId,
            damage = damage
        });
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        HurtBox hurtbox = col.GetComponent<HurtBox>();
        HurtBox myHurtbox = GetComponentInParent<HurtBox>();

        if (hurtbox == null || hurtbox == myHurtbox) return;

        // 타겟이 충돌 영역에서 벗어나면 다음 펀치를 위해 hasHit을 리셋합니다.
        hasHit = false;
    }

    private void OnDisable()
    {
        // OnDisable 시점에도 안전하게 리셋해주는 것이 좋습니다.
        hasHit = false;
    }
}
