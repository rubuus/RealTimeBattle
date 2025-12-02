using System;
using UnityEngine;
using static PlayerController;

public class EnemyController : MonoBehaviour
{
    private PlayerState enemyState = PlayerState.Idle;

    private Vector2 networkTargetPos;
    private string networkTargetState;
    public bool isNetworkUpdatePending = false;
    private float networkDir;

    private Vector2 smoothVel;
    [SerializeField] private float networkSmoothTime = 0.015f;

    private bool canReceiveNetwork = false;

    private Rigidbody2D rigid;
    private Animator anim;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rigid.bodyType = RigidbodyType2D.Kinematic;
        rigid.linearVelocity = Vector2.zero;
        networkDir = (SocketClient.Instance.side == "RIGHT") ? 1f : -1f;
    }

    void Update()
    {
        if (!canReceiveNetwork) return;

        if (isNetworkUpdatePending) 
        {
            ApplyServerStateWithGuard();
            ApplyNetworkPosition();
            ApplyServerDirection();
        } 
    }

    public void ApplyServerDirection()
    {
        transform.localScale = new Vector2(networkDir, 1);
    }

    public void ApplyServerStateWithGuard()
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

    public void ApplyServerState(Vector2 pos, string state, int dir)
    {
        networkTargetPos = pos;
        networkTargetState = state;
        networkDir = dir;
        isNetworkUpdatePending = true;
    }

    public void ApplyNetworkPosition()
    {
            transform.position = Vector2.SmoothDamp(
                transform.position,
                networkTargetPos,
                ref smoothVel,
                networkSmoothTime
            );
    }

    void ChangeState(PlayerState newState)
    {
        // 중복 전환 방지
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

    public void EnableNetwork()
    {
        canReceiveNetwork = true;
    }
}
