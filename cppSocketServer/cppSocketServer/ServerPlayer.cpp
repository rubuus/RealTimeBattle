#include "ServerPlayer.h"
#include "PlayerInputPacket.h"
#include <vector>
#include <cmath>
#include <string>

ServerPlayer::ServerPlayer(int32_t playerId, uint8_t playerSide, const std::pair<float, float>& spawnPosition)
	: id(playerId),
	side(playerSide),
	position(spawnPosition),
	dir(playerSide == 0 ? 1 : -1),
	prevY(spawnPosition.second)
{
	dash.duration = 0.1f;
	dash.speed = 30.0f;
	dash.cooldown = 0.5f;

	punch.duration = 0.2f;
	punch.cooldown = 0.4f;
}

Platform sceneSize(-9.0f, 9.0f, 5.0f);

void ServerPlayer::ApplyInput(const PlayerInputPacket& p) {
	moveInput = p.move;
	jumpPressed = p.jump == 1 ? true : false;
	dashPressed = p.dash == 1 ? true : false;
	punchPressed = p.punch == 1 ? true : false;
}

void ServerPlayer::Update() {
	UpdateTimer();
	UpdateStateMachine();
	UpdatePosition();
	CheckOnGround();
	UpdateDirection();

	prevY = position.second;
}

void ServerPlayer::UpdateTimer() {
	if (dash.cooldownTimer > 0.0f) dash.cooldownTimer -= FIXED_STEP;
	if (punch.cooldownTimer > 0.0f) punch.cooldownTimer -= FIXED_STEP;

	if (invincibleTimer > 0.0f)
	{
		invincibleTimer -= FIXED_STEP;

		if (invincibleTimer <= 0.0f)
			isInvincible = false;
	}
}

void ServerPlayer::UpdateDirection() {
	if (state == PlayerState::Punch)
		return;

	if (moveInput > 0.01f) SetDir(1);
	else if (moveInput < -0.01f) SetDir(-1);
}

void ServerPlayer::UpdateBaseState() {
	if (!onGround)
		state = PlayerState::Jump;
	else if (std::abs(moveInput) > 0.01f)
		state = PlayerState::Run;
	else
		state = PlayerState::Idle;
}

void ServerPlayer::UpdateStateMachine() {
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

void ServerPlayer::UpdatePosition() {

	if (!onGround &&
		state != PlayerState::GroundDash &&
		state != PlayerState::AirDash)
	{
		velocity.second += gravity * FIXED_STEP;
	}

	position.first += velocity.first * FIXED_STEP;
	position.second += velocity.second * FIXED_STEP;

	if (position.first > sceneSize.maxX)
		position.first = sceneSize.maxX;

	if (position.first < sceneSize.minX)
		position.first = sceneSize.minX;

	if (position.second > sceneSize.Y)
	{
		position.second = sceneSize.Y;
		velocity.second = 0.0f;
	}
}


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

void ServerPlayer::UpdateMove()
{
	// 수평 이동
	velocity.first = moveInput * moveSpeed;

	// 점프
	if (jumpPressed && jumpCount > 0)
	{

		jumpPressed = false;
		velocity.second = jumpPower;
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
	velocity.first = 0.0f;
}

void ServerPlayer::UpdatePunch() {
	punch.timer -= FIXED_STEP;

	if (punch.timer <= 0.0f)
		UpdateBaseState();
}

void ServerPlayer::TakeDamage(int damage, float hurtVel) {
	if (isInvincible)
		return;

	currentHP -= damage;
	if (currentHP < 0) currentHP = 0;

	// 피격 시 경직 + 무적
	hurtTimer = hurtDuration;
	invincibleTimer = hurtDuration;
	isInvincible = true;

	state = PlayerState::Hurt;
	velocity.first = hurtVel;
}

void ServerPlayer::UpdateHurt() {
	hurtTimer -= FIXED_STEP;

	if (hurtTimer <= 0.0f)
		UpdateBaseState();
}

void ServerPlayer::CheckOnGround() {
	float groundY = 0.0f;
	bool grounded = false;

	for (auto p : platforms)
	{
		if (position.first >= p.minX &&
			position.first <= p.maxX &&
			position.second <= p.Y &&
			prevY >= p.Y)   // 위에서 내려오는 경우에만 착지
		{
			groundY = p.Y;
			grounded = true;
			break;
		}
	}

	if (grounded)
	{
		position = { position.first, groundY };
		velocity = { velocity.first, 0.0f };
		onGround = true;
		jumpCount = 2;
	}
	else
	{
		onGround = false;
	}
}

PlayerStatePacket ServerPlayer::StatePacket() const
{
	return PlayerStatePacket {
		id,
		position.first,
		position.second,
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