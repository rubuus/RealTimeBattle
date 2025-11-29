using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using static UnityEngine.GraphicsBuffer;

public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Run,
        Jump,
        GroundDash,
        AirDash,
        Punch,
        Hurt
    }

    private PlayerState state = PlayerState.Idle;

    // --- 입력값 (로컬에서만 사용) ---
    private float moveInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool punchPressed;

    // --- 서버에서 받은 권위 좌표/상태 ---
    private Vector2 networkTargetPos;
    private string networkTargetState;
    private bool isNetworkUpdatePending = false;
    private float networkDir;
    [SerializeField] private float networkSmoothTime = 0.015f;

    private Vector2 smoothVel;

    private bool canReceiveNetwork = false;

    private Rigidbody2D rigid;
    private Animator anim;

    [SerializeField] private PunchHitbox punchHitbox;
    [SerializeField] private HurtBox hurtBox;
    private Vector2 originalHitboxPos;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rigid.bodyType = RigidbodyType2D.Kinematic;
        rigid.linearVelocity = Vector2.zero;
        networkDir = (SocketClient.Instance.side == "LEFT") ? 1f : -1f;
        originalHitboxPos = punchHitbox.transform.localPosition;
    }

    void Start()
    {
        hurtBox.Initialize(SocketClient.Instance.myUserId);
    }

    void Update()
    {
        ReadInput();

        if (!canReceiveNetwork) return;

        ApplyServerStateWithGuard();
        ApplyNetworkPosition();
        ApplyServerDirection();
        SyncHitboxDirection();
        SendInput();
    }

    void ReadInput()
    {
        if (Input.GetKey(KeyCode.RightArrow))
            moveInput = 1;
        else if (Input.GetKey(KeyCode.LeftArrow))
            moveInput = -1;
        else
            moveInput = 0;

        jumpPressed = Input.GetKeyDown(KeyCode.C);
        dashPressed = Input.GetKeyDown(KeyCode.X);
        punchPressed = Input.GetKeyDown(KeyCode.Z);
    }

    private void SendInput()
    {
        SocketClient.Instance.Send(new PlayerInputPacket()
        {
            type = "INPUT",
            id = SocketClient.Instance.myUserId,
            move = moveInput,
            jump = jumpPressed,
            dash = dashPressed,
            punch = punchPressed
        });
    }

    public void ApplyServerDirection()
    {
        transform.localScale = new Vector2(networkDir, 1);
    }

    public void SyncHitboxDirection()
    {
        punchHitbox.transform.localPosition = new Vector2(
                originalHitboxPos.x * networkDir,
                originalHitboxPos.y
            );
    }

    public void ApplyServerStateWithGuard()
    {
        if (isNetworkUpdatePending)
        {
            PlayerState newState =
                (PlayerState)Enum.Parse(typeof(PlayerState), networkTargetState);


            if (state == PlayerState.Punch && newState == PlayerState.Punch)
            {
                isNetworkUpdatePending = false;
                return;
            }

            ChangeState(newState);
            isNetworkUpdatePending = false;
        }
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
        if (state == newState)
            return;

        state = newState;

        switch (state)
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