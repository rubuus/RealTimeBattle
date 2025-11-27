using System;
using UnityEngine;
using static PlayerController;

public class EnemyController : MonoBehaviour
{
    private PlayerState enemyState = PlayerState.Idle;

    private Vector2 networkTargetPos;
    private string networkTargetState;
    public bool isNetworkUpdatePending = false;

    private Vector2 smoothVel;
    [SerializeField] private float networkSmoothTime = 0.015f;

    private bool canReceiveNetwork = false;

    private Rigidbody2D rigid;
    private Animator anim;

    [SerializeField] private PunchHitBox punchHitbox;
    private Vector2 originalHitboxPos;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rigid.bodyType = RigidbodyType2D.Kinematic;
        rigid.linearVelocity = Vector2.zero;
        originalHitboxPos = punchHitbox.transform.localPosition;
    }

    private void Update()
    {
        if (!canReceiveNetwork) return;

        float dx = networkTargetPos.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
        {
            transform.localScale = new Vector2(dx > 0 ? 1 : -1, 1);
        }

        punchHitbox.transform.localPosition = new Vector2(
            originalHitboxPos.x * transform.localScale.x,
            originalHitboxPos.y
        );

        if (isNetworkUpdatePending)
        {
            PlayerState newState =
                (PlayerState)Enum.Parse(typeof(PlayerState), networkTargetState);


            if (enemyState == PlayerState.Punch && newState == PlayerState.Punch)
            {
                isNetworkUpdatePending = false;
                return;
            }

            ChangeState(newState);
            isNetworkUpdatePending = false;
        }

        if (networkTargetPos != Vector2.zero)
        {
            if (Vector2.Distance(transform.position, networkTargetPos) < 0.005f)
                return;

            transform.position = Vector2.SmoothDamp(
                transform.position,
                networkTargetPos,
                ref smoothVel,
                networkSmoothTime
            );

        }
    }

    void ChangeState(PlayerState newState)
    {
        if (enemyState == newState)
            return;

        enemyState = newState;

        switch (enemyState)
        {
            case PlayerState.Idle:
                anim.Play("Idle");
                break;

            case PlayerState.Run:
                anim.Play("Run");
                break;

            case PlayerState.Jump:
                anim.Play("Jump");
                break;

            case PlayerState.GroundDash:
                anim.Play("GroundDash");
                break;

            case PlayerState.AirDash:
                anim.Play("AirDash");
                break;

            case PlayerState.Punch:
                anim.Play("Punch");
                break;

            case PlayerState.Hurt:
                anim.Play("Hurt");
                break;
        }
    }

    public void EnemyStateUpdate(Vector2 pos, string state)
    {
        if (!canReceiveNetwork) return;

        networkTargetPos = pos;
        networkTargetState = state;
        isNetworkUpdatePending = true;
    }

    public void EnableNetwork()
    {
        canReceiveNetwork = true;
    }
}
