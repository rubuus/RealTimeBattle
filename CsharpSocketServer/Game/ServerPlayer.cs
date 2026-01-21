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

public struct Dash
{
    public float Duration;
    public float Speed;
    public float Timer;
    public float Cooldown;
    public double CooldownTimer;
};

public struct Punch
{
    public float Duration;
    public float Timer;
    public float Cooldown;
    public double CooldownTimer;
};

public class ServerPlayer
{
    public int Id { get; }
    public string Side { get; }

    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }

    public short Direction { get; private set; }
    public PlayerState State { get; private set; } = PlayerState.Idle;

    public bool IsStateDirty { get; private set; } = false;
    public float MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool DashPressed { get; private set; }
    public bool PunchPressed { get; private set; }

    public float MoveSpeed { get; } = 17f;
    public float JumpPower { get; } = 18f;
    public float Gravity { get; } = -80f;

    private float FIXED_STEP;
    
    private float _prevX;
    private float _prevY;

    public bool OnGround { get; private set; }
    public int JumpCount { get; private set; } = 2;

    private Dash _dash;
    private Punch _punch;

    public bool PunchChecked { get; private set; } = false;
    public float HurtDuration { get; } = 0.2f;
    public float HurtTimer { get; private set; }

    public bool IsInvincible { get; private set; }
    public float InvincibleTimer { get; private set; }

    public int CurrentHp { get; private set; } = 100;

    private Platform _screenSize = new Platform { MinX = -9f, MaxX = 9f, Y = 5.0f };

    private List<Platform> Platforms = new List<Platform>
    {
        new Platform { MinX = -9f, MaxX = 9f, Y = -2.5f },
        new Platform { MinX = -6.7f, MaxX = -4.3f, Y = 2.5f },
        new Platform { MinX = -4.2f,  MaxX = -1.8f, Y = 0f },
        new Platform { MinX = -0.7f,  MaxX = 2.2f, Y = 1.5f },
        new Platform { MinX = 2.8f,  MaxX = 5.2f, Y = 3.5f },
        new Platform { MinX = 4.3f,  MaxX = 6.7f, Y = -0.5f },
    };

    public ServerPlayer(int id, string side, Vector2 spawnPos)
    {
        Id = id;
        Side = side;
        Direction = (Side == "LEFT") ?  (short)1 : (short)-1;

        Position = spawnPos;
        Velocity = Vector2.Zero;

        _prevX = Position.X;
        _prevY = Position.Y;

        _dash = new Dash
        {
            Duration = 0.2f,
            Speed = 20.0f,
            Cooldown = 1.0f
        };

        _punch = new Punch
        {
            Duration = 0.4f,
            Cooldown = 0.8f
        };
    }

    public void ApplyInput(PlayerInputPacket p)
    {
        float newMove = Math.Clamp(p.Move, -1.0f, 1.0f);
        MoveInput = newMove;

        if (p.Jump == true) JumpPressed = true;
        if (p.Dash == true) DashPressed = true;
        if (p.Punch == true) PunchPressed = true;
    }

    public void ClearStateDirty()
    {
        IsStateDirty = false;
    }

    public void SetDir(short d)
    {
        Direction = d;
    }

    public void SetPunchChecked(bool b)
    {
        PunchChecked = b;
    }

    // --- 틱마다 FSM + 물리 업데이트 ---
    public void Update(float dt)
    {
        FIXED_STEP = dt;
        PlayerState oldState = State; // 업데이트 전 상태 저장
        short oldDir = Direction;

        UpdateTimer();			// dash/punch 쿨타임 및 피격 무적 시간 갱신
        UpdateStateMachine();	// 현재 상태 반영
        UpdatePosition();		// 좌표 갱신
        CheckOnGround();		// OnGround 체크
        UpdateDirection();		// 방향 갱신
        
        // Jitter 값보다 크거나 이전 상태/방향과 다르면 true (실제로 움직임)
        if (Math.Abs(Position.X - _prevX) > 0.001f ||
            Math.Abs(Position.Y - _prevY) > 0.001f ||
            oldState != State ||
            oldDir != Direction)
        {
            IsStateDirty = true;
        }

        // 지속시간/쿨타임 중 선입력 방지
        JumpPressed = false;
        DashPressed = false;
        PunchPressed = false;

        _prevX = Position.X;
        _prevY = Position.Y;
    }

    private void UpdateBaseState()
    {
        if (!OnGround)
            State = PlayerState.Jump;
        else if (Math.Abs(MoveInput) > 0.01f)
            State = PlayerState.Run;
        else
            State = PlayerState.Idle;
    }

    private void UpdateTimer()
    {
        if (_dash.CooldownTimer > 0f) _dash.CooldownTimer -= FIXED_STEP;
        if (_punch.CooldownTimer > 0f) _punch.CooldownTimer -= FIXED_STEP;

        if (InvincibleTimer > 0f)
        {
            InvincibleTimer -= FIXED_STEP;

            if (InvincibleTimer <= 0f)
                IsInvincible = false;
        }
    }

    private void UpdateActionTriggers()
    {
        if (State == PlayerState.Idle ||
            State == PlayerState.Run ||
            State == PlayerState.Jump)
        {
            if (DashPressed && _dash.CooldownTimer <= 0f)
            {
                StartDash();
                return;
            }

            if (PunchPressed && _punch.CooldownTimer <= 0f)
            {
                StartPunch();
                return;
            }
        }
    }

    private void UpdateStateMachine()
    {
        UpdateActionTriggers();

        // 상태별 처리
        switch (State)
        {
            case PlayerState.Hurt:
                UpdateHurt();
                break;

            case PlayerState.Punch:
                UpdatePunch();
                break;

            case PlayerState.GroundDash:
            case PlayerState.AirDash:
                UpdateDash();
                break;

            default:
                UpdateMove();
                break;
        }
    }

    private void UpdatePosition()
    {
        if (!OnGround && 
        State != PlayerState.GroundDash &&
        State != PlayerState.AirDash)
        {
            Velocity = new Vector2(
                Velocity.X,
                Velocity.Y + Gravity * FIXED_STEP
            );
        }

        Position += Velocity * FIXED_STEP;

        if (Position.X > _screenSize.MaxX)
            Position = new Vector2(_screenSize.MaxX, Position.Y);
        
        if (Position.X < _screenSize.MinX)
            Position = new Vector2(_screenSize.MinX, Position.Y);

        if (Position.Y > _screenSize.Y)
        {
            Position = new Vector2(Position.X, _screenSize.Y);
            Velocity = new Vector2(Velocity.X, 0f);
        }
    }

    private void CheckOnGround()
    {
        float groundY = 0f;
        bool grounded = false;

        foreach (var p in Platforms)
        {
            if (Position.X >= p.MinX &&
                Position.X <= p.MaxX &&
                Position.Y <= p.Y &&
                _prevY >= p.Y)   // 위에서 내려오는 경우에만 착지
            {
                groundY = p.Y;
                grounded = true;
                break;
            }
        }

        if (grounded)
        {
            Position = new Vector2(Position.X, groundY);
            Velocity = new Vector2(Velocity.X, 0f);
            OnGround = true;
            JumpCount = 2;
        }
        else
        {
            OnGround = false;
        }
    }

    private void UpdateDirection()
    {
        if (State == PlayerState.Punch)
            return;

        if (MoveInput > 0.01f) Direction = 1;
        else if (MoveInput < -0.01f) Direction = -1;
    }

    // --- 일반 상태 (Idle / Run / Jump) ---
    private void UpdateMove()
    {
        // 수평 이동
        Velocity = new Vector2(MoveInput * MoveSpeed, Velocity.Y);

        // 점프
        if (JumpPressed && JumpCount > 0)
        {
            
            JumpPressed = false;
            Velocity = new Vector2(Velocity.X, JumpPower);
            JumpCount--;
        }

        UpdateBaseState();
    }

    // --- 대쉬 ---
    private void StartDash()
    {
        _dash.Timer = _dash.Duration;
        _dash.CooldownTimer = _dash.Cooldown;
        DashPressed = false;
        
        if (OnGround)
            State = PlayerState.GroundDash;
        else
            State = PlayerState.AirDash;
            
        Velocity = new Vector2(Direction * _dash.Speed, 0f);
    }

    private void UpdateDash()
    {
        _dash.Timer -= FIXED_STEP;

        if (_dash.Timer <= 0f)
            UpdateBaseState();
    }

    // --- 펀치 ---
    private void StartPunch()
    {
        _punch.Timer = _punch.Duration;
        _punch.CooldownTimer = _punch.Cooldown;
        PunchChecked = false;
        State = PlayerState.Punch;
        Velocity = new Vector2(0f, Velocity.Y);
    }

    private void UpdatePunch()
    {
        _punch.Timer -= FIXED_STEP;

        if (_punch.Timer <= 0f)
            UpdateBaseState();
    }

    // --- 피격 ---
    public void TakeDamage(int damage, float hurtVel)
    {
        if (IsInvincible)
            return;

        CurrentHp -= damage;
        if (CurrentHp < 0) CurrentHp = 0;

        // 피격 시 경직 + 무적
        HurtTimer = HurtDuration;
        InvincibleTimer = HurtDuration;
        IsInvincible = true;

        State = PlayerState.Hurt;
        Velocity = new Vector2(hurtVel, Velocity.Y);
    }

    private void UpdateHurt()
    {
        HurtTimer -= FIXED_STEP;

        if (HurtTimer <= 0f)
            UpdateBaseState();
    }

    // --- 클라로 보낼 상태 패킷 생성 ---
    public PlayerStatePacket StatePacket()
    {
        return new PlayerStatePacket
        {
            Type = "PLAYER_STATE",
            UserId = Id,
            X = Position.X,
            Y = Position.Y,
            State = State.ToString(),
            Dir = Direction
        };
    }

    public DamagePacket HurtPacket()
    {
        return new DamagePacket
        {
            Type = "TAKE_DAMAGE",
            HurtId = Id,
            CurrentHP = CurrentHp
        };
    }
}
