#pragma once
#include <string>
#include <vector>
#include "Platform.h"
#include "PlayerStatePacket.h"
#include "DamagePacket.h"

class Platform;
struct PlayerInputPacket;
struct PlayerStatePacket;
struct DamagePacket;

enum class PlayerState : uint8_t
{
    Idle = 0,
    Run = 1,
    Jump = 2,
    GroundDash = 3,
    AirDash = 4,
    Punch = 5,
    Hurt = 6
};

struct Dash
{
    float duration;
    float speed;
    float timer;
    float cooldown;
    float cooldownTimer;
};

struct Punch
{
    float duration;
    float timer;
    float cooldown;
    float cooldownTimer;
};

class ServerPlayer
{
public:
	ServerPlayer(int32_t playerId, 
        uint8_t playerSide, 
        const std::pair<float, float>& spawnPosition
        );

    void ApplyInput(const PlayerInputPacket& p);
	void Update();
    void TakeDamage(int damage, float hurtVel);
    PlayerStatePacket StatePacket() const;
	DamagePacket HurtPacket() const;

	PlayerState GetState() const { return state; }
	void SetState(PlayerState s) { state = s; }

	int GetDir() const { return dir; }
    void SetDir(int8_t d) { dir = d; }

	bool GetOnGround() const { return onGround; }
    void SetOnGround(bool b) { onGround = b; }

	bool GetJumpPressed() const { return jumpPressed; }
    void SetJumpPressed(bool b) { jumpPressed = b; }

	bool GetDashPressed() const { return dashPressed; }
    void SetDashPressed(bool b) { dashPressed = b; }

	bool GetPunchPressed() const { return punchPressed; }
    void SetPunchPressed(bool b) { punchPressed = b; }

	bool HasPunchChecked() const { return punchChecked; }
	void SetPunchChecked(bool b) { punchChecked = b; }
	
	std::pair<float, float> GetPosition() const { return position; }
    void SetPosition(std::pair<float, float> pos) { position = pos; }
    
    std::pair<float, float> GetVelocity() const { return velocity; }
    void SetVelocity(std::pair<float, float> vel) { velocity = vel; }
	
	int GetCurrentHP() const { return currentHP; }
    void SetCurrentHP(int hp) { currentHP = hp; }

private:
    void UpdatePosition();
    void UpdateDirection();
    void UpdateBaseState();
    void UpdateTimer();
    void UpdateStateMachine();
    void UpdateActionTriggers();
    void UpdateMove();
    void StartDash();
    void UpdateDash();
    void StartPunch();
    void UpdatePunch();
    void UpdateHurt();
    void CheckOnGround();

    float FIXED_STEP = 1 / 60;

    int32_t id;
    uint8_t side;
    std::pair<float, float> position;
    std::pair<float, float> velocity = { 0.0, 0.0 };
    int8_t dir;
    PlayerState state = PlayerState::Idle;

    // 입력 (한 틱 동안 유지)
    float moveInput = 0.0f;
    bool jumpPressed = false;
    bool dashPressed = false;
    bool punchPressed = false;

    // 파라미터
    float moveSpeed = 17.0f;
    float jumpPower = 18.0f;
    float gravity = -80.0f;
    float prevY;

    // 점프/대쉬/펀치/피격 관련
    bool onGround = false;
    int jumpCount = 2;

    bool punchChecked = false;

    float hurtDuration = 0.2f;
    float hurtTimer = 0.0f;

    bool isInvincible = false;
    float invincibleTimer = 0.0f;

    int currentHP = 100;

    Dash dash;
    Punch punch;

    std::vector<Platform> platforms = {
        { -9.0f, 9.0f, -2.5f },
        { -6.7f, -4.3f, 2.5f },
        { -4.2f, -1.8f, 0.0f },
        { -0.7f, 2.2f, 1.5f },
        { 2.8f, 5.2f, 3.5f },
        { 4.3f, 6.7f, -0.5f }
    };
};