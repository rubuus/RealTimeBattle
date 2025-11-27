using UnityEngine;

public class PunchHitBox : MonoBehaviour
{
    public int damage = 10;
    private bool hasHit = false;

    private void OnEnable()
    {
        hasHit = false;
    }

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

    private void OnDisable()
    {
        hasHit = false; 
    }
}
