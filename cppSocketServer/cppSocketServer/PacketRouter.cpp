#include <iostream>
#include "PacketRouter.h"
#include "PacketHeader.h"
#include "LoginPacket.h"
#include "Server.h"
#include "Room.h"
#include "RoomEvent.h"

PacketRouter& PacketRouter::Instance()
{
    static PacketRouter instance;
    return instance;
}

void PacketRouter::Route(ClientSession& s, const ParsedPacket& pkt)
{
    switch (static_cast<C2S_PacketType>(pkt.type))
    {
        case C2S_PacketType::LOGIN:
		    HandleLogin(s, pkt.body, pkt.bodySize);
            break;

        case C2S_PacketType::MATCH_START:
            HandleMatchStart(s);
            break;

        case C2S_PacketType::BATTLE_READY:
		    HandleBattleReady(s);
            break;

        case C2S_PacketType::BATTLE_START:
		    HandleBattleStart(s);
            break;

        case C2S_PacketType::INPUT:
		    HandleInput(s, pkt.body, pkt.bodySize);
            break;

        case C2S_PacketType::RESULT_ACK:
		    HandleResultAck(s);
            break;

        case C2S_PacketType::PING:
		    HandlePing(s);
            break;

        default:
            s.Disconnect();
            break;
    }
}

void PacketRouter::HandleLogin(ClientSession& s, const char* body, uint16_t bodySize)
{
    // 바디 사이즈 체크
    if (bodySize < sizeof(LoginPacket))
        return;

	// 패킷에 직접 접근하지 않고, 안전하게 memcpy 사용
    LoginPacket login;
    memcpy(&login, body, sizeof(LoginPacket));

    s.SetUserId(login.userId);
}

void PacketRouter::HandleMatchStart(ClientSession& s)
{
    Server::Instance().AddToMatchList(s.GetSessionId());
};

void PacketRouter::HandleBattleReady(ClientSession& s)
{
    Room* room = s.GetRoom();
    if (!room) return;

    RoomEvent ev{};
    ev.type = RoomEventType::BattleReady;
    ev.sessionId = s.GetSessionId();

    room->EnqueueEvent(ev);
};

void PacketRouter::HandleBattleStart(ClientSession& s)
{
    Room* room = s.GetRoom();
    if (!room) return;

    RoomEvent ev{};
    ev.type = RoomEventType::BattleStart;
    ev.sessionId = s.GetSessionId();

    room->EnqueueEvent(ev);
};

void PacketRouter::HandleInput(ClientSession& s, const char* body, uint16_t bodySize)
{
    // 바디 사이즈 체크
    if (bodySize < sizeof(PlayerInputPacket))
        return;

    Room* room = s.GetRoom();
    if (!room) return;

    // 패킷에 직접 접근하지 않고, 안전하게 memcpy 사용
    PlayerInputPacket input;
    memcpy(&input, body, sizeof(input));

    RoomEvent ev{};
    ev.type = RoomEventType::PlayerInput;
    ev.sessionId = s.GetSessionId();
	ev.input = input;

    room->EnqueueEvent(ev);
};

void PacketRouter::HandleResultAck(ClientSession& s)
{
    Room* room = s.GetRoom();
    if (!room) return;

    RoomEvent ev{};
    ev.type = RoomEventType::ResultAck;
    ev.sessionId = s.GetSessionId();

    room->EnqueueEvent(ev);
};

void PacketRouter::HandlePing(ClientSession& s)
{
    s.SetLastRecvTime();
};