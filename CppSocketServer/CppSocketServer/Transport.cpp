#include <iostream>
#include "Transport.h"
#include "Server.h"
#include "ClientSession.h"
#include "RoomEvent.h"
#include "PacketHeader.h"

void Transport::Dispatch(const RoomOutEvent& ev)
{
	auto* session = Server::Instance().FindSession(ev.sessionId);

	// 이벤트 처리 시점에 세션이 이미 종료되었을 수 있으므로 null 체크
	if (!session) return;

	switch (ev.type)
	{
		case RoomOutEventType::LoadBattle:
			SendReadyRoom(ev);
			break;

		case RoomOutEventType::PlayerSpawn:
			SendSpawn(ev);
			break;

		case RoomOutEventType::StateUpdate:
			SendState(ev);
			break;

		case RoomOutEventType::TimeUpdate:
			SendTime(ev);
			break;

		case RoomOutEventType::Attack:
			SendDamage(ev);
			break;

		case RoomOutEventType::GameResult:
			SendResult(ev);
			break;

		case RoomOutEventType::EnemyExit:
			SendEnemyExit(ev);
			break;

		case RoomOutEventType::CloseRoom:
			SendRoomClosed(ev);
			break;

		default:
			break;
	}
}

// 룸 생성 시, 해당 세션에 패킷 전송
void Transport::SendReadyRoom(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	s->SendPacket(S2C_HeaderType::LOAD_BATTLE);
}

// 해당 세션에 스폰용으로 상태 패킷 한번 전송
void Transport::SendSpawn(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<UpdateStatePayload>(ev.payload);

	s->SendPacket(S2C_HeaderType::PLAYER_STATE, payload.p1);
	s->SendPacket(S2C_HeaderType::PLAYER_STATE, payload.p2);
}

// 해당 세션에 상태 패킷 전송
void Transport::SendState(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<UpdateStatePayload>(ev.payload);

	s->SendPacket(S2C_HeaderType::PLAYER_STATE, payload.p1);
	s->SendPacket(S2C_HeaderType::PLAYER_STATE, payload.p2);
}

// 해당 세션에 시간 패킷 전송
void Transport::SendTime(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<UpdateTimePayload>(ev.payload);

	s->SendPacket(S2C_HeaderType::GAME_TIME, payload.time);
}

// 해당 세션에 데미지 패킷 전송
void Transport::SendDamage(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<UpdateHurtPayload>(ev.payload);

	s->SendPacket(S2C_HeaderType::TAKE_DAMAGE, payload.dmg);
}

// 해당 세션에 결과 패킷 한번 전송
void Transport::SendResult(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<GameResultPayload>(ev.payload);

	std::cout << ev.sessionId << '\n';
	if (payload.winner == -1)
	{
		s->SendPacket(S2C_HeaderType::GAME_DRAW);
	}
	else if (payload.winner == ev.sessionId)
	{
		s->SendPacket(S2C_HeaderType::GAME_WIN);
	}
	else
	{
		s->SendPacket(S2C_HeaderType::GAME_LOSE);
	}
}

// 해당 세션에 상대 종료 패킷 전송
void Transport::SendEnemyExit(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	s->SendPacket(S2C_HeaderType::ENEMY_EXIT);
}

// 해당 세션에 룸 닫힘 패킷 전송
void Transport::SendRoomClosed(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	s->SendPacket(S2C_HeaderType::ROOM_CLOSED);
}