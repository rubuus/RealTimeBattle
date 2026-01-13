#include <iostream>
#include "Network.h"
#include "Server.h"
#include "ClientSession.h"
#include "RoomEvent.h"
#include "PacketHeader.h"

void Network::Dispatch(const RoomOutEvent& ev)
{
	switch (ev.type)
	{
		case RoomOutEventType::LoadBattle:
			BroadcastReadyRoom(ev);
			break;

		case RoomOutEventType::PlayerSpawn:
			BroadcastSpawn(ev);
			break;

		case RoomOutEventType::StateUpdate:
			BroadcastState(ev);
			break;

		case RoomOutEventType::TimeUpdate:
			BroadcastTime(ev);
			break;

		case RoomOutEventType::Attack:
			BroadcastDamage(ev);
			break;

		case RoomOutEventType::GameResult:
			BroadcastResult(ev);
			break;

		case RoomOutEventType::EnemyExit:
			BroadcastEnemyExit(ev);
			break;

		case RoomOutEventType::CloseRoom:
			BroadcastRoomClosed(ev);
			break;

		default:
			break;
	}
}

void Network::BroadcastReadyRoom(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	s->SendPacket(S2C_PacketType::LOAD_BATTLE);
}

void Network::BroadcastSpawn(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<UpdateStatePayload>(ev.payload);

	s->SendPacket(S2C_PacketType::PLAYER_STATE, payload.p1);
	s->SendPacket(S2C_PacketType::PLAYER_STATE, payload.p2);
}

void Network::BroadcastState(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<UpdateStatePayload>(ev.payload);

	s->SendPacket(S2C_PacketType::PLAYER_STATE, payload.p1);
	s->SendPacket(S2C_PacketType::PLAYER_STATE, payload.p2);
}

void Network::BroadcastTime(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<UpdateTimePayload>(ev.payload);

	s->SendPacket(S2C_PacketType::GAME_TIME, payload.time);
}

void Network::BroadcastDamage(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<UpdateHurtPayload>(ev.payload);

	s->SendPacket(S2C_PacketType::TAKE_DAMAGE, payload.dmg);
}

void Network::BroadcastResult(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	auto& payload = std::get<GameResultPayload>(ev.payload);

	if (payload.winner == -1)
	{
		s->SendPacket(S2C_PacketType::GAME_DRAW);
	}
	else if (payload.winner == ev.sessionId)
	{
		s->SendPacket(S2C_PacketType::GAME_WIN);
	}
	else
	{
		s->SendPacket(S2C_PacketType::GAME_LOSE);
	}
}

void Network::BroadcastEnemyExit(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	s->SendPacket(S2C_PacketType::ENEMY_EXIT);
}

void Network::BroadcastRoomClosed(const RoomOutEvent& ev)
{
	auto* s = Server::Instance().FindSession(ev.sessionId);
	s->SendPacket(S2C_PacketType::ROOM_CLOSED);
}