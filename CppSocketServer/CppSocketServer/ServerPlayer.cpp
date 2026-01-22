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
#include <iostream>

ServerPlayer::ServerPlayer(int32_t playerId, uint8_t playerSide, Vector2 spawnPosition)
	: id(playerId),
	side(playerSide),
	position(spawnPosition),
	dir(playerSide == 0 ? 1 : -1),
	prevX(spawnPosition.x),
	prevY(spawnPosition.y)
{
	dash.duration = 0.2f;
	dash.speed = 20.0f;
	dash.cooldown = 1.0f;

	punch.duration = 0.4f;
	punch.cooldown = 0.8f;
}

void ServerPlayer::ApplyInput(const PlayerInputPacket& p) {
	float newMove = std::clamp(p.move, -1.0f, 1.0f);
	moveInput = newMove;

	if (p.jump == 1) jumpPressed = true;
	if (p.dash == 1) dashPressed = true;
	if (p.punch == 1) punchPressed = true;
}

// 플레이어 업데이트
void ServerPlayer::Update(double dt) {
	FIXED_STEP = dt;
	PlayerState oldState = state; // 업데이트 전 상태 저장
	int8_t oldDir = dir;

	UpdateTimer();			// dash/punch 쿨타임 및 피격 무적 시간 갱신
	UpdateStateMachine();	// 현재 상태 반영
	UpdatePosition();		// 좌표 갱신
	CheckOnGround();		// OnGround 체크
	UpdateDirection();		// 방향 갱신

	// Jitter 값보다 크거나 이전 상태/방향과 다르면 true (실제로 움직임)
	if (std::abs(position.x - prevX) > 0.001f ||
		std::abs(position.y - prevY) > 0.001f ||
		oldState != state ||
		oldDir != dir)
	{
		stateDirty = true;
	}

	// 지속시간/쿨타임 중 선입력 방지
	jumpPressed = false;
	dashPressed = false;
	punchPressed = false;

	prevX = position.x;
	prevY = position.y;
}

void ServerPlayer::UpdateTimer() {
	if (dash.cooldownTimer > 0.0f)
		dash.cooldownTimer -= FIXED_STEP;
	
	if (punch.cooldownTimer > 0.0f)
		punch.cooldownTimer -= FIXED_STEP;

	if (invincibleTimer > 0.0f)
	{
		invincibleTimer -= FIXED_STEP;

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
	// 수평 이동: 입력이 있으면 속도 부여, 없으면 정지
	if (std::abs(moveInput) > 0.01f)
		velocity.x = moveInput * moveSpeed;
	else velocity.x = 0.0f;

	if (jumpPressed && jumpCount > 0)
	{
		velocity.y = jumpPower;
		jumpCount--;
		jumpPressed = false;
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
	invincibleTimer = 1.5f;
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