using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

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

    public bool isLocalPlayer = true;

    private float moveSpeed = 6.0f;
    private float jumpForce = 15.0f;
    private int jumpCount = 2;
    private float moveInput = 0;
    private bool jumpPressed = false;

    private bool onGround = true;
    public bool isColliding = false;

    public float dashSpeed = 20f;
    public float dashTime = 5f;
    public float dashCooldown = 0.5f;
    float dashTimer = 0f;
    float dashCooldownTimer = 0f;

    float punchTimer = 0f;
    public float punchDuration = 0.4f;   // 펀치 애니 길이(클립 기준)
    public float punchCooldown = 0.2f;   // 펀치 쿨타임
    float punchCooldownTimer = 0f;

    public bool isLeftSide;
    private bool canReceiveNetwork = false;

    private Color enemyColor = new Color(0f, 1f, 1f);

    private Vector2 _networkTargetPos; // 서버로부터 받은 최종 목표 위치
    private string _networkTargetState; // 서버로부터 받은 최종 목표 상태
    public bool isNetworkUpdatePending = false;
    private Vector2 smoothVel = Vector2.zero;
    [SerializeField] private float networkSmoothTime = 0.02f;
    private float networkActionEndTime = 0f;

    private Rigidbody2D rigid;
    private Animator anim;

    [SerializeField] private PunchHitBox punchHitbox;
    private Vector2 originalHitboxPos;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rigid.gravityScale = 4.0f;
        originalHitboxPos = punchHitbox.transform.localPosition;
    }

    void Update()
    {
        if (!isLocalPlayer)
        {
            // 1. 상태 업데이트 처리
            if (isNetworkUpdatePending)
            {
                PlayerState newState = (PlayerState)Enum.Parse(typeof(PlayerState), _networkTargetState);

                // ----------------------------------------------------------------------------------
                // ★ 애니메이션 덮어쓰기 방지 핵심 로직 ★

                // 1. 현재 Punch/Dash 상태이고, 아직 로컬 애니메이션 종료 시간이 지나지 않았는데
                // 2. 서버에서 Idle, Run, Jump 등 액션을 취소하는 상태가 왔다면 (Hurt는 제외)
                if (networkActionEndTime > Time.time)
                {
                    if (this.state == PlayerState.Punch || this.state == PlayerState.GroundDash || this.state == PlayerState.AirDash)
                    {
                        // 서버에서 받은 상태가 취소 상태(Idle, Run 등)일 경우 무시
                        if (newState != PlayerState.Hurt && newState != this.state)
                        {
                            // 새로운 패킷을 받았지만, 현재 애니메이션이 끝나기를 기다림.
                            isNetworkUpdatePending = false;
                            return;
                        }
                    }
                }
                // ----------------------------------------------------------------------------------

                // 공격 중일 때 같은 공격 상태는 무시 (기존 로직 유지)
                if (this.state == PlayerState.Punch && newState == PlayerState.Punch)
                    return;

                ChangeState(newState);

                isNetworkUpdatePending = false;
            }

            // 2. 위치 보간 (클라이언트 틱: Update에서 실행)
            if (_networkTargetPos != Vector2.zero)
            {
                // ★ 애니메이션 간섭 방지 로직 추가 ★
                // 펀치, 대시 등의 강제 이동 상태에서는 위치 보간을 건너뛰어 애니메이션이 위치를 제어하게 함.
                // 이 상태들은 FixedUpdate에서 일정 시간이 지나면 Idle/Run으로 복귀할 것이므로, 
                // 그 이후부터는 다시 위치 보간이 적용됩니다.
                if (this.state == PlayerState.Punch ||
                    this.state == PlayerState.GroundDash ||
                    this.state == PlayerState.AirDash)
                {
                    // 위치 업데이트를 건너뛰지만, 방향은 유지
                    transform.localScale = new Vector2(Mathf.Sign(transform.localScale.x), 1);
                    return;
                }

                // ★ SmoothDamp 재도입 (떨림 Jitter 해결에 Lerp보다 효과적) ★
                transform.position = Vector2.SmoothDamp(
                    transform.position,
                    _networkTargetPos,
                    ref smoothVel, // ref 변수를 사용하여 부드러운 전환 속도 제어
                    networkSmoothTime // 0.02f (빠른 보간 시간으로 지연 최소화)
                );
            }
            return;
        }
        // --- 로컬 플레이어 로직 (기존 유지) ---
        HandleInput();

        if (moveInput > 0) transform.localScale = new Vector2(1, 1);
        else if (moveInput < 0) transform.localScale = new Vector2(-1, 1);

        // 히트박스 좌표 갱신
        punchHitbox.transform.localPosition = new Vector2(
            originalHitboxPos.x * transform.localScale.x,
            originalHitboxPos.y
        );

        PlayerMovePacket movePacket = new PlayerMovePacket()
        {
            type = "PLAYER_MOVE",
            id = SocketClient.Instance.myUserId,
            x = transform.position.x,
            y = transform.position.y,
            state = state.ToString(),
            dir = (int)Mathf.Sign(transform.localScale.x)
        };

        string json = JsonUtility.ToJson(movePacket);
        SocketClient.Instance.Send(json);
    }

    void FixedUpdate()
    {
        // ... (FixedUpdate 이하 기존 로직은 네트워크 플레이어에게는 적용되지 않으므로 그대로 유지)
        HandleGroundCheck();

        if (onGround)
            jumpCount = 1;

        // 쿨타임 감소
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.fixedDeltaTime;
        if (punchCooldownTimer > 0f)
            punchCooldownTimer -= Time.fixedDeltaTime;

        // ----- D A S H  -----
        if (state == PlayerState.GroundDash || state == PlayerState.AirDash)
        {
            HandleDash();
            return;
        }

        HandleMovement();
        HandleJump();
        HandleStopOnCollision();

        // ----- P U N C H -----
        if (state == PlayerState.Punch)
        {
            HandlePunching();
            return;
        }

        // ----- I D L E / R U N / J U M P -----
        if (state != PlayerState.GroundDash &&
            state != PlayerState.AirDash &&
            state != PlayerState.Punch)
        {
            if (!onGround)
                ChangeState(PlayerState.Jump);
            else if (Mathf.Abs(moveInput) > 0.01f)
                ChangeState(PlayerState.Run);
            else
                ChangeState(PlayerState.Idle);
        }
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                sr.color = enemyColor;
        }
    }

    void HandleInput()
    {
        // 이동 입력
        if (Input.GetKey(KeyCode.RightArrow))
            moveInput = 1;
        else if (Input.GetKey(KeyCode.LeftArrow))
            moveInput = -1;
        else
            moveInput = 0;

        // 점프 입력
        if (Input.GetKeyDown(KeyCode.C) && jumpCount > 0)
            jumpPressed = true;
            

        if (Input.GetKeyDown(KeyCode.X) &&
            dashCooldownTimer <= 0f &&
            state != PlayerState.Punch &&
            state != PlayerState.GroundDash &&
            state != PlayerState.AirDash)
        {
            if (onGround)
                StartGroundDash();
            else
                StartAirDash();
        }

        // 펀치 입력
        if (Input.GetKeyDown(KeyCode.Z) &&
            punchCooldownTimer <= 0f &&
            state != PlayerState.Punch &&
            state != PlayerState.GroundDash &&
            state != PlayerState.AirDash)
        {
            StartPunch();
        }
    }

    void ChangeState(PlayerState newState)
    {
        // 중복 전환 방지
        if (state == newState)
            return;

        state = newState;

        if (!isLocalPlayer)
        {
            float duration = 0f;

            if (newState == PlayerState.Punch)
                duration = punchDuration;
            else if (newState == PlayerState.GroundDash || newState == PlayerState.AirDash)
                duration = dashTime;

            if (duration > 0f)
            {
                // 현재 시간 + 지속 시간으로 종료 시간 설정
                networkActionEndTime = Time.time + duration;
            }
            else
            {
                networkActionEndTime = 0f;
            }
        }

        switch (newState)
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

    void HandleMovement()
    {
        // ★ 이동/중력/상태 변경 금지
        if (state == PlayerState.Punch ||
            state == PlayerState.GroundDash ||
            state == PlayerState.AirDash)
            return;

        rigid.linearVelocity = new Vector2(moveInput * moveSpeed, rigid.linearVelocity.y);

        if (!onGround)
            ChangeState(PlayerState.Jump);
        else if (Mathf.Abs(moveInput) > 0.01f)
            ChangeState(PlayerState.Run);
        else
            ChangeState(PlayerState.Idle);
    }



    void HandleGroundCheck()
    {
        float extraHeight = 0.2f;
        Collider2D col = GetComponent<Collider2D>();

        Vector2 origin = new Vector2(col.bounds.center.x, col.bounds.min.y);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, extraHeight, LayerMask.GetMask("Ground"));

        Debug.DrawRay(origin, Vector2.down * extraHeight, Color.green);

        onGround = hit.collider != null;
    }

    void HandleJump()
    {
        if (!jumpPressed) return;
        if (!onGround && jumpCount <= 0) return;

        jumpPressed = false;
        jumpCount--;

        Vector2 v = rigid.linearVelocity;
        v.y = 0;
        rigid.linearVelocity = v;

        rigid.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        ChangeState(PlayerState.Jump);
    }


    void HandleStopOnCollision()
    {
        if (!onGround) return;
        if (!isColliding) return;

        rigid.linearVelocity = new Vector2(0, rigid.linearVelocity.y);
    }

    void StartGroundDash()
    {
        ChangeState(PlayerState.GroundDash);

        dashTimer = dashTime;
        dashCooldownTimer = dashCooldown;

        rigid.gravityScale = 4; // 지상은 중력 켜둠

        float dir = transform.localScale.x;
        rigid.linearVelocity = new Vector2(dir * dashSpeed, 0);
    }


    void StartAirDash()
    {
        ChangeState(PlayerState.AirDash);

        dashTimer = dashTime;
        dashCooldownTimer = dashCooldown;

        rigid.gravityScale = 0; // 공중 대쉬는 중력 OFF

        float dir = transform.localScale.x;
        rigid.linearVelocity = new Vector2(dir * dashSpeed, 0);
    }


    void HandleDash()
    {
        dashTimer -= Time.fixedDeltaTime;

        // 대쉬 중에는 지속적으로 전진
        float dir = Mathf.Sign(transform.localScale.x);
        rigid.linearVelocity = new Vector2(dir * dashSpeed, 0);

        // 공중대쉬는 중력 제거 / 지상은 유지
        if (state == PlayerState.AirDash)
            rigid.gravityScale = 0;
        else
            rigid.gravityScale = 4;

        // ---- 종료 ----
        if (dashTimer <= 0f)
        {
            rigid.gravityScale = 4; // 중력 복구

            if (state == PlayerState.AirDash)
            {
                // 공중에서 끝났으면 낙하 모션
                ChangeState(PlayerState.Jump);
            }
            else
            {
                // 지상에서 끝났으면 Idle/Run
                if (Mathf.Abs(moveInput) > 0.01f)
                    ChangeState(PlayerState.Run);
                else
                    ChangeState(PlayerState.Idle);
            }
        }
    }

    void StartPunch()
    {
        ChangeState(PlayerState.Punch);

        punchTimer = punchDuration;
        punchCooldownTimer = punchDuration + punchCooldown;

        punchHitbox.gameObject.SetActive(true);

        if (onGround)
            rigid.linearVelocity = Vector2.zero;
    }

    void HandlePunching()
    {
        punchTimer -= Time.fixedDeltaTime;

        if (punchTimer <= 0f)
        {
            punchHitbox.gameObject.SetActive(false);

            // 펀치 종료 후 상태 복귀
            if (!onGround)
                ChangeState(PlayerState.Jump);
            else if (Mathf.Abs(moveInput) > 0.01f)
                ChangeState(PlayerState.Run);
            else
                ChangeState(PlayerState.Idle);
        }
    }

    public void OnHurt()
    {
        ChangeState(PlayerState.Hurt);
    }

    public void EnemyStateUpdate(Vector2 pos, string state)
    {
        if (!canReceiveNetwork) return;
        if (isLocalPlayer) return;

        // 1. 상태 및 위치 정보 저장
        _networkTargetPos = pos;
        _networkTargetState = state;
        isNetworkUpdatePending = true; // 새로운 데이터가 있음을 표시
    }

    public void EnableNetwork()
    {
        canReceiveNetwork = true;
    }
}
