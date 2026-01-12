#include <vector>
#include <cmath>
#include <string>
#include <algorithm>
#include "Platform.h"
#include "PlayerStatePacket.h"
#include "PlayerInputPacket.h"
#include "DamagePacket.h"
#include "PlayerStruct.h"
#include "ServerPlayer.h"

ServerPlayer::ServerPlayer(int32_t playerId, uint8_t playerSide, Vector2 spawnPosition)
	: id(playerId),
	side(playerSide),
	position(spawnPosition),
	dir(playerSide == 0 ? 1 : -1),
	prevY(spawnPosition.y)
{
	dash.duration = 0.1f;
	dash.speed = 30.0f;
	dash.cooldown = 0.5f;

	punch.duration = 0.2f;
	punch.cooldown = 0.4f;
}

void ServerPlayer::ApplyInput(const PlayerInputPacket& p) {
	moveInput = std::clamp(p.move, -1.0f, 1.0f);
	jumpPressed = p.jump == 1 ? true : false;
	dashPressed = p.dash == 1 ? true : false;
	punchPressed = p.punch == 1 ? true : false;
}

// 플레이어 업데이트
void ServerPlayer::Update() {
	UpdateTimer();			// dash/punch 쿨타임 및 피격 무적 시간 갱신
	UpdateStateMachine();	// 현재 상태 반영
	UpdatePosition();		// 좌표 갱신
	CheckOnGround();		// OnGround 체크
	UpdateDirection();		// 방향 갱신

	prevY = position.y;
}

void ServerPlayer::UpdateTimer() {
	if (dash.cooldownTimer > 0.0f) 
		dash.cooldownTimer = std::max(0.0f, dash.cooldownTimer - FIXED_STEP);
	
	if (punch.cooldownTimer > 0.0f)
		punch.cooldownTimer = std::max(0.0f, punch.cooldownTimer - FIXED_STEP);

	if (invincibleTimer > 0.0f)
	{
		invincibleTimer = std::max(0.0f, punch.cooldownTimer - FIXED_STEP);

		if (invincibleTimer <= 0.0f)
			isInvincible = false;
	}
}

void ServerPlayer::UpdateStateMachine() {

	// 액션 트리거 발동 체크
	UpdateActionTriggers();

	// 상태별 처리
	switch (state)
	{
	case PlayerState::Hurt:
		UpdateHurt();
		break;

	case PlayerState::Punch:
		UpdatePunch();
		break;

	case PlayerState::GroundDash:
	case PlayerState::AirDash:
		UpdateDash();
		break;

	default:
		UpdateMove();
		break;
	}
}

// BaseState 시, Dash or Punch 입력 확인
void ServerPlayer::UpdateActionTriggers()
{
	if (state == PlayerState::Idle ||
		state == PlayerState::Run ||
		state == PlayerState::Jump)
	{
		if (dashPressed && dash.cooldownTimer <= 0.0f)
		{
			StartDash();
			return;
		}

		if (punchPressed && punch.cooldownTimer <= 0.0f)
		{
			StartPunch();
			return;
		}
	}
}

void ServerPlayer::UpdatePosition() {

	// 공중 + 대쉬 아닐 시, 낙하
	if (!onGround &&
		state != PlayerState::GroundDash &&
		state != PlayerState::AirDash)
	{
		velocity.y += gravity * FIXED_STEP;
	}

	position.x += velocity.x * FIXED_STEP;
	position.y += velocity.y * FIXED_STEP;

	// 맵 width 넘어가는거 방지
	if (position.x > sceneSize.maxX)
		position.x = sceneSize.maxX;

	if (position.x < sceneSize.minX)
		position.x = sceneSize.minX;

	// 맵 height 넘어가는거 방지
	if (position.y > sceneSize.Y)
	{
		position.y = sceneSize.Y;
		velocity.y = 0.0f;
	}
}

// 발판 및 땅 체크
void ServerPlayer::CheckOnGround() {
	float groundY = 0.0f;
	bool grounded = false;

	for (const auto& p : platforms)
	{
		if (position.x >= p.minX &&
			position.x <= p.maxX &&
			position.y <= p.Y &&
			prevY >= p.Y)   // 위에서 내려오는 경우에만 착지
		{
			groundY = p.Y;
			grounded = true;
			break;
		}
	}

	if (grounded)
	{
		position = { position.x, groundY };
		velocity = { velocity.x, 0.0f };
		onGround = true;
		jumpCount = 2;
	}
	else onGround = false;
}

void ServerPlayer::UpdateDirection() {
	// punch 중일때는 방향 못 바꿈
	if (state == PlayerState::Punch)
		return;

	if (moveInput > 0.01f) SetDir(1);
	else if (moveInput < -0.01f) SetDir(-1);
}

void ServerPlayer::UpdateMove()
{
	// 수평 이동
	velocity.x = moveInput * moveSpeed;

	// 점프
	if (jumpPressed && jumpCount > 0)
	{

		jumpPressed = false;
		velocity.y = jumpPower;
		jumpCount--;
	}

	UpdateBaseState();
}

void ServerPlayer::StartDash() {
	dash.timer = dash.duration;
	dash.cooldownTimer = dash.cooldown;
	dashPressed = false;

	if (onGround)
		state = PlayerState::GroundDash;
	else
		state = PlayerState::AirDash;

	velocity = { dir * dash.speed, 0.0f };
}

void ServerPlayer::UpdateDash() {
	dash.timer -= FIXED_STEP;

	if (dash.timer <= 0.0f)
		UpdateBaseState();
}

void ServerPlayer::StartPunch() {
	punch.timer = punch.duration;
	punch.cooldownTimer = punch.cooldown;
	state = PlayerState::Punch;
	punchChecked = false;
	punchPressed = false;
	velocity.x = 0.0f;
}

void ServerPlayer::UpdatePunch() {
	punch.timer -= FIXED_STEP;

	if (punch.timer <= 0.0f)
		UpdateBaseState();
}

void ServerPlayer::TakeDamage(int damage, float hurtVel) {
	// 이미 무적 상태면 끝내기
	if (isInvincible)
		return;

	currentHP -= damage;
	if (currentHP < 0) currentHP = 0;

	// 피격 시 경직 + 무적
	hurtTimer = hurtDuration;
	invincibleTimer = hurtDuration;
	isInvincible = true;

	// 피격 시 뒤로 밀림
	state = PlayerState::Hurt;
	velocity.x = hurtVel;
}

void ServerPlayer::UpdateHurt() {
	hurtTimer -= FIXED_STEP;

	if (hurtTimer <= 0.0f)
		UpdateBaseState();
}

// 기본 상태 업데이트
void ServerPlayer::UpdateBaseState() {
	if (!onGround)
		state = PlayerState::Jump;
	else if (std::abs(moveInput) > 0.01f)
		state = PlayerState::Run;
	else
		state = PlayerState::Idle;
}

PlayerStatePacket ServerPlayer::StatePacket() const
{
	return PlayerStatePacket {
		id,
		position.x,
		position.y,
		static_cast<uint8_t>(state),
		dir
	};
}

DamagePacket ServerPlayer::HurtPacket() const
{
	return DamagePacket {
		id,
		currentHP
	};
}