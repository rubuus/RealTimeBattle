using System;
using System.Collections.Generic;
using System.Numerics;

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

public class ServerPlayer
{
    public int id;
    public string side;  
    // 권위 상태
    public Vector2 position;
    public Vector2 velocity;

    public int dir;
    public PlayerState state = PlayerState.Idle;

    // 입력 (한 틱 동안 유지)
    public float moveInput;
    public bool jumpPressed;
    public bool dashPressed;
    public bool punchPressed;

    // 파라미터
    public float moveSpeed = 17f;
    public float jumpPower = 18f;
    public float gravity = -80f;
    float prevY;

    // 점프/대쉬/펀치/피격 관련
    public bool onGround = false;
    public int jumpCount = 2;

    public struct Dash
    {
        public float duration;
        public float speed;
        public float timer;
        public float cooldown;
        public float cooldownTimer;
    }

    public struct Punch
    {
        public float duration;
        public float timer;
        public float cooldown;
        public float cooldownTimer;
    }

    public float hurtDuration = 0.2f;
    public float hurtTimer = 0f;

    public bool isInvincible = false;
    public float invincibleTimer = 0f;

    public int currentHP = 100;

    public Dash dash;
    public Punch punch;

    List<Platform> platforms = new List<Platform>
    {
        new Platform { xMin = -9f, xMax = 9f, y = -2.5f },
        new Platform { xMin = -6.7f, xMax = -4.3f, y = 2.5f },
        new Platform { xMin = -4.2f,  xMax = -1.8f, y = 0f },
        new Platform { xMin = -0.7f,  xMax = 2.2f, y = 1.5f },
        new Platform { xMin = 2.8f,  xMax = 5.2f, y = 3.5f },
        new Platform { xMin = 4.3f,  xMax = 6.7f, y = -0.5f },
    };

    public ServerPlayer(int userId, string side, Vector2 spawnPosition)
    {
        id = userId;
        this.side = side;
        dir = (side == "LEFT") ? 1 : -1;
        position = spawnPosition;
        prevY = spawnPosition.Y;
        onGround = true;
        jumpCount = 2;

        dash.duration = 0.1f;
        dash.speed = 30f;
        dash.cooldown = 0.5f;

        punch.duration = 0.2f;
        punch.cooldown = 0.4f;
    }

    // --- 입력 적용 (패킷 받을 때마다 호출) ---
    public void ApplyInput(PlayerInputPacket p)
    {
        moveInput = p.move;
        jumpPressed = p.jump;
        dashPressed = p.dash;
        punchPressed = p.punch;
    }

    // --- 틱마다 FSM + 물리 업데이트 ---
    public void Update(float dt)
    {
        UpdateTimer(dt);
        UpdateStateMachine(dt);
        UpdatePosition(dt);
        CheckOnGround();
        UpdateDirection();
        
        prevY = position.Y;
    }

    void UpdateBaseState()
    {
         if (!onGround)
            state = PlayerState.Jump;
        else if (Math.Abs(moveInput) > 0.01f)
            state = PlayerState.Run;
        else
            state = PlayerState.Idle;
    }

    void UpdatePosition(float dt)
    {
        Platform sceneSize = new Platform {xMin = -9f, xMax = 9f, y = 5f};

        if (!onGround && 
        state != PlayerState.GroundDash &&
        state != PlayerState.AirDash)
        {
            velocity.Y += gravity * dt;
        }

        position += velocity * dt;

        if (position.X > sceneSize.xMax)
            position.X = sceneSize.xMax;
        
        if (position.X < sceneSize.xMin)
            position.X = sceneSize.xMin;

        if (position.Y > sceneSize.y)
        {
            position.Y = sceneSize.y;
            velocity.Y = 0f;
        }
    }

    void UpdateDirection()
    {
        if (state == PlayerState.Punch)
            return;

        if (moveInput > 0.01f) dir = 1;
        else if (moveInput < -0.01f) dir = -1;
    }

    void UpdateTimer(float dt)
    {
        if (dash.cooldownTimer > 0f) dash.cooldownTimer -= dt;
        if (punch.cooldownTimer > 0f) punch.cooldownTimer -= dt;

        if (invincibleTimer > 0f)
        {
            invincibleTimer -= dt;

            if (invincibleTimer <= 0f)
                isInvincible = false;
        }
    }

    void UpdateStateMachine(float dt)
    {
        UpdateActionTriggers();

        // 상태별 처리
        switch (state)
        {
            case PlayerState.Hurt:
                UpdateHurt(dt);
                break;

            case PlayerState.Punch:
                UpdatePunch(dt);
                break;

            case PlayerState.GroundDash:
            case PlayerState.AirDash:
                UpdateDash(dt);
                break;

            default:
                UpdateMove(dt);
                break;
        }
    }

    // --- 일반 상태 (Idle / Run / Jump) ---
    void UpdateMove(float dt)
    {
        // 수평 이동
        velocity.X = moveInput * moveSpeed;

        // 점프
        if (jumpPressed && jumpCount > 0)
        {
            
            jumpPressed = false;
            velocity.Y = jumpPower;
            jumpCount--;
        }

        UpdateBaseState();
    }

    void UpdateActionTriggers()
    {
        if (state == PlayerState.Idle ||
            state == PlayerState.Run ||
            state == PlayerState.Jump)
        {
            if (dashPressed && dash.cooldownTimer <= 0f)
            {
                StartDash();
                return;
            }

            if (punchPressed && punch.cooldownTimer <= 0f)
            {
                StartPunch();
                return;
            }
        }
    }

    // --- 대쉬 ---
    void StartDash()
    {
        dash.timer = dash.duration;
        dash.cooldownTimer = dash.cooldown;
        dashPressed = false;
        
        if (onGround)
            state = PlayerState.GroundDash;
        else
            state = PlayerState.AirDash;
            
        velocity = new Vector2(dir * dash.speed, 0f);
    }

    void UpdateDash(float dt)
    {
        dash.timer -= dt;

        if (dash.timer <= 0f)
            UpdateBaseState();
    }

    // --- 펀치 ---
    void StartPunch()
    {
        punch.timer = punch.duration;
        punch.cooldownTimer = punch.cooldown;
        state = PlayerState.Punch;
        velocity.X = 0f;
    }

    void UpdatePunch(float dt)
    {
        punch.timer -= dt;

        if (punch.timer <= 0f)
            UpdateBaseState();
    }

    // --- 피격 ---
    public void TakeDamage(int damage, float hurtVel)
    {
        if (isInvincible)
            return;

        currentHP -= damage;
        if (currentHP < 0) currentHP = 0;

        // 피격 시 경직 + 무적
        hurtTimer = hurtDuration;
        invincibleTimer = hurtDuration;
        isInvincible = true;

        state = PlayerState.Hurt;
        velocity.X = hurtVel;
    }

    void UpdateHurt(float dt)
    {
        hurtTimer -= dt;

        if (hurtTimer <= 0f)
            UpdateBaseState();
    }

    void CheckOnGround()
    {
        float groundY = 0f;
        bool grounded = false;

        foreach (var p in platforms)
        {
            if (position.X >= p.xMin &&
                position.X <= p.xMax &&
                position.Y <= p.y &&
                prevY >= p.y)   // 위에서 내려오는 경우에만 착지
            {
                groundY = p.y;
                grounded = true;
                break;
            }
        }

        if (grounded)
        {
            position = new Vector2(position.X, groundY);
            velocity = new Vector2(velocity.X, 0f);
            onGround = true;
            jumpCount = 2;
        }
        else
        {
            onGround = false;
        }
    }

    // --- 클라로 보낼 상태 패킷 생성 ---
    public PlayerStatePacket StatePacket()
    {
        return new PlayerStatePacket
        {
            type = "PLAYER_STATE",
            userId = id,
            x = position.X,
            y = position.Y,
            state = state.ToString(),
            dir = dir
        };
    }

    public DamagePacket HurtPacket()
    {
        return new DamagePacket
        {
            type = "TAKE_DAMAGE",
            hurtId = id,
            currentHP = currentHP
        };
    }
}
