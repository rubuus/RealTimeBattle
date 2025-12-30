#include "ServerPlayer.h"
#include "PlayerInputPacket.h"
#include <vector>
#include <cmath>
#include <string>

ServerPlayer::ServerPlayer(int playerId, std::string playerSide, std::pair<float, float> spawnPosition)
	: id(playerId),
	side(playerSide),
	position(spawnPosition),
	dir(playerSide == "LEFT" ? 1 : -1),
	prevY(spawnPosition.second)
{
	dash.duration = 0.1f;
	dash.speed = 30.0f;
	dash.cooldown = 0.5f;

	punch.duration = 0.2f;
	punch.cooldown = 0.4f;
}

void ServerPlayer::ApplyInput(const PlayerInputPacket& p) {
	moveInput = p.move;
	jumpPressed = p.jump;
	dashPressed = p.dash;
	punchPressed = p.punch;
}

void ServerPlayer::Update(float dt) {
	UpdateTimer(dt);
	UpdateStateMachine(dt);
	UpdatePosition(dt);
	CheckOnGround();
	UpdateDirection();

	prevY = position.second;
}

void ServerPlayer::UpdateTimer(float dt) {
	if (dash.cooldownTimer > 0.0f) dash.cooldownTimer -= dt;
	if (punch.cooldownTimer > 0.0f) punch.cooldownTimer -= dt;

	if (invincibleTimer > 0.0f)
	{
		invincibleTimer -= dt;

		if (invincibleTimer <= 0.0f)
			isInvincible = false;
	}
}

void ServerPlayer::UpdateDirection() {
	if (state == PlayerState::Punch)
		return;

	if (moveInput > 0.01f) dir = 1;
	else if (moveInput < -0.01f) dir = -1;
}

void ServerPlayer::UpdateBaseState() {
	if (!onGround)
		state = PlayerState::Jump;
	else if (abs(moveInput) > 0.01f)
		state = PlayerState::Run;
	else
		state = PlayerState::Idle;
}

void ServerPlayer::UpdateStateMachine(float dt) {
	UpdateActionTriggers();

	// 상태별 처리
	switch (state)
	{
		case PlayerState::Hurt:
			UpdateHurt(dt);
			break;

		case PlayerState::Punch:
			UpdatePunch(dt);
			break;

		case PlayerState::GroundDash:
		case PlayerState::AirDash:
			UpdateDash(dt);
			break;

		default:
			UpdateMove(dt);
			break;
	}
}

void ServerPlayer::UpdatePosition(float dt) {
	Platform* sceneSize = new Platform(-9.0f, 9.0f, 5.0f);

	if (!onGround &&
		state != PlayerState::GroundDash &&
		state != PlayerState::AirDash)
	{
		velocity.second += gravity * dt;
	}

	position.first += velocity.first * dt;
	position.second += velocity.second * dt;

	if (position.first > sceneSize->maxX)
		position.first = sceneSize->maxX;

	if (position.first < sceneSize->minX)
		position.first = sceneSize->minX;

	if (position.second > sceneSize->Y)
	{
		position.second = sceneSize->Y;
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

void ServerPlayer::UpdateMove(float dt)
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

void ServerPlayer::UpdateDash(float dt) {
	dash.timer -= dt;

	if (dash.timer <= 0.0f)
		UpdateBaseState();
}

void ServerPlayer::StartPunch() {
	punch.timer = punch.duration;
	punch.cooldownTimer = punch.cooldown;
	state = PlayerState::Punch;
	velocity.first = 0.0f;
}

void ServerPlayer::UpdatePunch(float dt) {
	punch.timer -= dt;

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

void ServerPlayer::UpdateHurt(float dt) {
	hurtTimer -= dt;

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

PlayerStatePacket ServerPlayer::StatePacket()
{
	return PlayerStatePacket {
		id,
		position.first,
		position.second,
		static_cast<uint8_t>(state),
		dir
	};
}

DamagePacket ServerPlayer::HurtPacket()
{
	return DamagePacket {
		id,
		currentHP
	};
}