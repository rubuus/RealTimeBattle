#include <iostream>
#define JWT_DISABLE_PICOJSON
#include <jwt-cpp/jwt.h>
#include <jwt-cpp/traits/nlohmann-json/defaults.h>
#include "PacketRouter.h"
#include "PacketHeader.h"
#include "Server.h"
#include "Room.h"
#include "RoomEvent.h"
#include "LoginTestBody.h"

PacketRouter& PacketRouter::Instance()
{
    static PacketRouter instance;
    return instance;
}

void PacketRouter::Route(ClientSession& s, const ParsedPacket& pkt)
{
    switch (static_cast<C2S_HeaderType>(pkt.type))
    {
        case C2S_HeaderType::LOGIN:
		    HandleLogin(s, pkt.body, pkt.bodySize);
            break;

        case C2S_HeaderType::MATCH_START:
            HandleMatchStart(s);
            break;

        case C2S_HeaderType::MATCH_CANCEL:
            HandelMatchCancel(s);
            break;

        case C2S_HeaderType::BATTLE_READY:
		    HandleBattleReady(s);
            break;

        case C2S_HeaderType::BATTLE_START:
            HandleBattleStart(s);
            break;

        case C2S_HeaderType::INPUT:
		    HandleInput(s, pkt.body, pkt.bodySize);
            break;

        case C2S_HeaderType::RESULT_ACK:
		    HandleResultAck(s);
            break;

        case C2S_HeaderType::PING:
		    HandlePing(s);
            break;

        case C2S_HeaderType::LOGIN_TEST:
            HandleLoginTest(s, pkt.body, pkt.bodySize);
            break;

        default:
            s.Disconnect("Not Found Packet Header");
            break;
    }
}

// 해당 세션에 JWT 검증 후, userId 초기화
// 클라이언트에서 보낸 토큰의 가변 크기 항상 신뢰
void PacketRouter::HandleLogin(ClientSession& s, const char* body, uint16_t bodySize)
{
    // 스택 변수로 저장
    std::string token(body, body + bodySize);

    // 바디 사이즈 검증
    if (bodySize == 0 || bodySize > 2048)
    {
        s.Disconnect("invalid jwt size");
        return;
    }

    // JWT Token 인증
    try
    {
        auto decoded = jwt::decode(token);

        jwt::verify()
            .allow_algorithm(jwt::algorithm::hs256{ Server::Instance().GetJWTKey() })
            .with_issuer("GamePortfolio.Server")
            .with_audience("GameClient")
            .verify(decoded);

        int userId = std::stoi(decoded.get_payload_claim("sub").as_string());

        std::mutex onlineMutex;
        {
            std::lock_guard<std::mutex> lock(onlineMutex);
            if (onlineUsers.find(userId) != onlineUsers.end())
            {
                s.Disconnect("Duplicate Login");
            }

            onlineUsers.insert(userId);
            s.SetUserId(userId);
        }
    }
    catch (const std::exception& e)
    {
        std::cerr << "[JWT ERROR] " << e.what() << "\n";
        s.Disconnect("jwt verify failed");
    }
}

// 매칭 시작 시, List에 해당 세션 추가
void PacketRouter::HandleMatchStart(ClientSession& s)
{
    Server::Instance().AddToMatchList(s.GetSessionId());
};

void PacketRouter::HandelMatchCancel(ClientSession& s)
{
    Server::Instance().CancelMatch(s.GetSessionId());
};

// 준비 완료되면 해당 룸에 이벤트 전달
void PacketRouter::HandleBattleReady(ClientSession& s)
{
    int roomId = s.GetRoomId();
    if (roomId == -1) return;

    RoomEvent ev{};
    ev.type = RoomEventType::BattleReady;
    ev.sessionId = s.GetSessionId();

    if (Room* r = Server::Instance().FindRoom(roomId))
    {
        r->EnqueueEvent(ev); // 여기서만 잠깐 사용
    }
};

// 배틀 씬 로드되면 해당 룸에 이벤트 전달
void PacketRouter::HandleBattleStart(ClientSession& s)
{
    int roomId = s.GetRoomId();
    if (roomId == -1) return;

    RoomEvent ev{};
    ev.type = RoomEventType::BattleStart;
    ev.sessionId = s.GetSessionId();

    if (Room* r = Server::Instance().FindRoom(roomId))
    {
        r->EnqueueEvent(ev); // 여기서만 잠깐 사용
    }
};

// 입력 있을 시, 해당 룸에 이벤트 전달
void PacketRouter::HandleInput(ClientSession& s, const char* body, uint16_t bodySize)
{
    // 바디 사이즈 체크
    if (bodySize < sizeof(PlayerInputPacket))
        return;

    int roomId = s.GetRoomId();
    if (roomId == -1) return;

    // 패킷에 직접 접근하지 않고, 안전하게 memcpy 사용
    PlayerInputPacket input;
    memcpy(&input, body, sizeof(input));

    RoomEvent ev{};
    ev.type = RoomEventType::PlayerInput;
    ev.sessionId = s.GetSessionId();
	ev.input = input;

    if (Room* r = Server::Instance().FindRoom(roomId))
    {
        r->EnqueueEvent(ev); // 여기서만 잠깐 사용
    }
};

// 배틀이 끝났다면 해당 룸에 이벤트 전달
void PacketRouter::HandleResultAck(ClientSession& s)
{
    int roomId = s.GetRoomId();
    if (roomId == -1) return;

    RoomEvent ev{};
    ev.type = RoomEventType::ResultAck;
    ev.sessionId = s.GetSessionId();

    if (Room* r = Server::Instance().FindRoom(roomId))
    {
        r->EnqueueEvent(ev); // 여기서만 잠깐 사용
    }
};

// 해당 세션 Heartbeat 시간 초기화
void PacketRouter::HandlePing(ClientSession& s)
{
    s.SetLastRecvTime();
};

// 봇 테스트 전용 (jwt 검증 X)
void PacketRouter::HandleLoginTest(ClientSession& s, const char* body, uint16_t bodySize)
{
    // 바디 사이즈 검증
    if (bodySize == 0 || bodySize > 2048)
    {
        s.Disconnect("invalid jwt size");
        return;
    }
    auto* data = reinterpret_cast<const LoginTestBody*>(body);
    int userId = data->userId;

    std::mutex onlineMutex;
    {
        std::lock_guard<std::mutex> lock(onlineMutex);
        if (onlineUsers.find(userId) != onlineUsers.end())
        {
            s.Disconnect("Duplicate Login");
        }

        onlineUsers.insert(userId);
        s.SetUserId(userId);
    }
}