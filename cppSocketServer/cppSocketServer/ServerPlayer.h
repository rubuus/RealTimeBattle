#pragma once
#include <string>
#include <vector>
#include "Platform.h"

class Platform;
struct PlayerInputPacket;
struct PlayerStatePacket;
struct DamagePacket;
struct PlayerStruct;

class ServerPlayer
{
public:
	ServerPlayer(int32_t playerId, 
        uint8_t playerSide, 
        Vector2 spawnPosition
        );

    void ApplyInput(const PlayerInputPacket& p);
	void Update();
    void TakeDamage(int damage, float hurtVel);

    // 패킷 생성
    PlayerStatePacket StatePacket() const;
	DamagePacket HurtPacket() const;

	PlayerState GetState() const { return state; }
	void SetState(PlayerState s) { state = s; }

    Vector2 GetPosition() const { return position; }
    void SetPosition(Vector2 v) { position = v; }

	int GetDir() const { return dir; }
    void SetDir(int8_t d) { dir = d; }

    bool HasPunchChecked() { return punchChecked; }
    void SetPunchChecked(bool b) { punchChecked = b; }
	
	int GetCurrentHP() const { return currentHP; }
    void SetCurrentHP(int hp) { currentHP = hp; }

private:
    // 업데이트 내부 함수
    void UpdateTimer();
    void UpdateStateMachine();
    void UpdateActionTriggers();
    void UpdatePosition();
    void CheckOnGround();
    void UpdateDirection();
    
    // State 변경 및 반영
    void UpdateMove();
    void StartDash();
    void UpdateDash();
    void StartPunch();
    void UpdatePunch();
    void UpdateHurt();
    
    // 기본 State 반영
    void UpdateBaseState();

private:
    float FIXED_STEP = 1 / 60;

    int32_t id;
    uint8_t side;
    Vector2 position;
    Vector2 velocity = { 0.0, 0.0 };
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

    int32_t currentHP = 100;

    Dash dash;
    Punch punch;

    Platform sceneSize{ -9.0f, 9.0f, 5.0f };

    std::vector<Platform> platforms = {
        { -9.0f, 9.0f, -2.5f },
        { -6.7f, -4.3f, 2.5f },
        { -4.2f, -1.8f, 0.0f },
        { -0.7f, 2.2f, 1.5f },
        { 2.8f, 5.2f, 3.5f },
        { 4.3f, 6.7f, -0.5f }
    };
};