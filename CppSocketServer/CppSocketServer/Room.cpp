#include <memory>
#include <cmath>
#include <iostream>
#include <nlohmann/json.hpp>
#include "ClientSession.h"
#include "Room.h"
#include "Transport.h"
#include "ServerPlayer.h"
#include "ApiClient.h"
#include "SaveRecordRequest.h"
#include "MatchFoundPacket.h"
#include "RoomEvent.h"
#include "ThreadPool.h"
#include "PlayerStruct.h"
#include "Server.h"

Room::Room(int id, int player1,int player2, ThreadPool& pool)
    : roomId(id), p1sid(player1), p2sid(player2), threadPool(pool)
{
    Vector2 leftSpawn = { -7.0f, -2.5f };
    Vector2 rightSpawn = { 7.0f, -2.5f };

    sp1 = std::make_unique<ServerPlayer>(
        p1UserId,
        static_cast<int8_t>(Side::Left),
        leftSpawn
    );

    sp2 = std::make_unique<ServerPlayer>(
        p2UserId,
        static_cast<int8_t>(Side::Right),
        rightSpawn
    );
}

// Network/IO 스레드 → Room 스레드로 전달되는 이벤트 enqueue
void Room::EnqueueEvent(const RoomEvent& ev)
{
    {
        std::lock_guard<std::mutex> lock(eventMutex);
        eventQueue.push(ev);
    }
    
    // 이벤트 큐 원소가 50개 이상이면 틱 밀리는 룸 로그 띄우기
    size_t sz;
    {
        std::lock_guard<std::mutex> lock(eventMutex);
        sz = eventQueue.size();
    }

    if (sz > 50)
    {
        auto now = std::chrono::steady_clock::now();

        if (now - lastLog > std::chrono::seconds(1))
        {
            std::cout
                << "[ROOM EVENT BACKLOG] room=" << roomId
                << " size=" << sz << "\n";
            lastLog = now;
        }
    }
}

// Network/IO 스레드로 전달되는 이벤트 enqueue
void Room::EmitOutEvent(const RoomOutEvent& ev)
{
    {
        std::lock_guard<std::mutex> lock(outEventMutex);
        outEventQueue.push(ev);
    }
    
    size_t sz;
    {
        std::lock_guard<std::mutex> lock(outEventMutex);
        sz = outEventQueue.size();
    }

    if (sz > 20)
    {
        auto now = std::chrono::steady_clock::now();

        if (now - lastLog > std::chrono::seconds(1))
        {
            std::cout
                << "[ROOM OUT BACKLOG] room=" << roomId
                << " size=" << sz << "\n";
            lastLog = now;
        }
    }
}

// 이벤트 큐 처리 (룸 업데이트 스레드에서 호출)
void Room::InputEvents()
{
    {
        std::lock_guard<std::mutex> lock(eventMutex);

        while (!eventQueue.empty())
        {
            auto ev = eventQueue.front();
            eventQueue.pop();

            switch (ev.type)
            {
            case RoomEventType::BattleReady:
                CheckMatch(ev);
                break;

            case RoomEventType::BattleStart:
                PlayerSpawn(ev);
                break;

            case RoomEventType::PlayerInput:
                OnInput(ev);
                break;

            case RoomEventType::ResultAck:
                OnAckReceived(ev);
                break;

            case RoomEventType::Disconnect:
                OnPlayerDisconnect(ev);
                break;

            default:
                break;
            }
        }
    }
}

// 이벤트 큐 처리 (룸 업데이트 스레드에서 호출)
void Room::OutEvents()
{
    {
        std::lock_guard<std::mutex> lock(outEventMutex);

        while (!outEventQueue.empty())
        {
            auto ev = outEventQueue.front();
            outEventQueue.pop();
            net.Dispatch(ev);
        }
    }
}

void Room::Update(double dt)
{
    InputEvents();
    OutEvents();

    if (!gameStarted) return;   // 매치 안됐으면 return
    if (!Server::Instance().FindSession(p1sid) ||
        !Server::Instance().FindSession(p2sid)) return;     // 세션 1명이라도 없으면 return
    if (!battleStarted) return; // 배틀씬 아니면 return

    gameTime -= dt;

    ServerPlayerUpdate(dt);

    stateSendAcc += dt;
    if (stateSendAcc >= 0.033)
        EmitStateUpdate();

    timeSendAcc += dt;
    if (timeSendAcc >= 1.0f)
        EmitTimeUpdate();

    EmitDamageUpdate();

    if (gameTime <= 0.0f || sp1->GetCurrentHP() <= 0 || sp2->GetCurrentHP() <= 0)
        EndGame();
}

void Room::CheckMatch(const RoomEvent& re)
{
    if (gameStarted) return;

    ready.insert(re.sessionId);

    if (ready.size() < 2) return;

    gameStarted = true;

    EmitOutEvent({ RoomOutEventType::LoadBattle, p1sid });
    EmitOutEvent({ RoomOutEventType::LoadBattle, p2sid });
}

void Room::PlayerSpawn(const RoomEvent& re)
{
    battleStarted = true;

    auto s1 = sp1->StatePacket();
    auto s2 = sp2->StatePacket();

    EmitOutEvent({ RoomOutEventType::PlayerSpawn, p1sid, UpdateStatePayload{ s1, s2 } });
    EmitOutEvent({ RoomOutEventType::PlayerSpawn, p2sid, UpdateStatePayload{ s1, s2 } });
}

// Player로직에 Input 값 넘겨주기
void Room::OnInput(const RoomEvent& re)
{
    if (re.sessionId == p1sid)
        sp1->ApplyInput(re.input);
    else if (re.sessionId == p2sid)
        sp2->ApplyInput(re.input);
}

void Room::ServerPlayerUpdate(double dt) 
{
    sp1->Update(dt);
    sp2->Update(dt);
}

void Room::EmitStateUpdate()
{
    stateSendAcc = 0.0f;

    if (!sp1->IsStateDirty() && !sp2->IsStateDirty())
        return;

    auto s1 = sp1->StatePacket();
    auto s2 = sp2->StatePacket();

    EmitOutEvent({ RoomOutEventType::StateUpdate, p1sid, UpdateStatePayload{ s1, s2 } });
    EmitOutEvent({ RoomOutEventType::StateUpdate, p2sid, UpdateStatePayload{ s1, s2 } });

    sp1->ClearStateDirty();
    sp2->ClearStateDirty();
}

void Room::EmitTimeUpdate()
{
    timeSendAcc -= 1.0f;

    auto t = static_cast<int32_t>(std::ceil(gameTime));

    EmitOutEvent({ RoomOutEventType::TimeUpdate, p1sid, UpdateTimePayload {t} });
    EmitOutEvent({ RoomOutEventType::TimeUpdate, p2sid, UpdateTimePayload {t} });
}

void Room::EmitDamageUpdate() {

    if (CheckDamage(*sp1, *sp2)) 
    {
        auto d2 = sp2->HurtPacket();
        EmitOutEvent({ RoomOutEventType::Attack, p1sid, UpdateHurtPayload { d2 } });
        EmitOutEvent({ RoomOutEventType::Attack, p2sid, UpdateHurtPayload { d2 } });
    }
    else if (CheckDamage(*sp2, *sp1))
    {
        auto d1 = sp1->HurtPacket();
        EmitOutEvent({ RoomOutEventType::Attack, p1sid, UpdateHurtPayload { d1 } });
		EmitOutEvent({ RoomOutEventType::Attack, p2sid, UpdateHurtPayload { d1 } });
    }
}

bool Room::CheckDamage(ServerPlayer& attacker, ServerPlayer& target)
{
	// Punch 상태가 아니면 데미지 판정 안 함
    if (attacker.GetState() != PlayerState::Punch)
        return false;

	// 이미 판정이 체크된 상태면 데미지 판정 안 함
    if (attacker.HasPunchChecked())
        return false;

    attacker.SetPunchChecked(true);

	// 펀치 범위에 상대가 없으면 데미지 판정 안 함
    if (!IsInPunchRange(attacker, target))
        return false;

	// 피격 방향 설정
    if (attacker.GetPosition().x > target.GetPosition().x && target.GetDir() < 0)
        target.SetDir(1);
    else if (attacker.GetPosition().x < target.GetPosition().x && target.GetDir() > 0)
        target.SetDir(-1);

    target.TakeDamage(10, attacker.GetDir() * 1.0f);

    return true;
}

// hitbox와 hurtbox 위치 및 크기 생성
bool Room::IsInPunchRange(ServerPlayer& attacker, ServerPlayer& target)
{
    Hitbox hitbox;
    Hurtbox hurtbox;

    hitbox.x = attacker.GetPosition().x + (0.3f * attacker.GetDir());
    hitbox.y = attacker.GetPosition().y;
    hitbox.halfW = 0.3f;
    hitbox.halfH = 0.7f;

    hurtbox.x = target.GetPosition().x;
    hurtbox.y = target.GetPosition().y + 0.5f;
    hurtbox.halfW = 0.5f;
    hurtbox.halfH = 0.5f;

    return Overlap(hitbox, hurtbox);
}

// hitbox와 hurtbox 겹침 판정
bool Room::Overlap(Hitbox& hit, Hurtbox& hurt)
{
    float diffX = std::abs(hit.x - hurt.x);
    float diffY = std::abs(hit.y - hurt.y);
    float limitX = hit.halfW + hurt.halfW;
    float limitY = hit.halfH + hurt.halfH;

    return (diffX <= limitX) && (diffY <= limitY);
}

bool Room::IsBotUser(int userId)
{
    return userId <= 0;
}

void Room::EndGame()
{
    if (closed.exchange(true))
        return;

    EmitGameResult();
    BeginCloseAckPhase();
}

void Room::EmitGameResult()
{
    int p1hp = sp1->GetCurrentHP();
    int p2hp = sp2->GetCurrentHP();

    if (p1hp > p2hp)
    {
        EmitOutEvent({ RoomOutEventType::GameResult, p1sid, GameResultPayload { p1sid } });
        EmitOutEvent({ RoomOutEventType::GameResult, p2sid, GameResultPayload { p1sid } });
        SaveRecordAsync(p1UserId, p2UserId);
    }
    else if (p1hp < p2hp)
    {
        EmitOutEvent({ RoomOutEventType::GameResult, p1sid, GameResultPayload { p2sid } });
        EmitOutEvent({ RoomOutEventType::GameResult, p2sid, GameResultPayload { p2sid } });
        SaveRecordAsync(p2UserId, p1UserId);
    }
    else
    {
        EmitOutEvent({ RoomOutEventType::GameResult, p1sid, GameResultPayload { -1 } });
        EmitOutEvent({ RoomOutEventType::GameResult, p2sid, GameResultPayload { -1 } });
        SaveRecordAsync(p1UserId, p2UserId);
    }
}

// 게임 종료 후 ACK 패킷 대기 단계 시작
void Room::BeginCloseAckPhase()
{
    ackReceivedMap.clear();
    ackReceivedMap[p1sid] = false;
    ackReceivedMap[p2sid] = false;

    waitingAckCount = 2;

    EmitOutEvent({ RoomOutEventType::CloseRoom, p1sid });
    EmitOutEvent({ RoomOutEventType::CloseRoom, p2sid });
}

// 클라이언트에서 ACK 패킷 수신 시 종료 플래그 처리
void Room::OnAckReceived(const RoomEvent& re)
{
    int sid = re.sessionId;
    auto it = ackReceivedMap.find(sid);

    if (it == ackReceivedMap.end())
        return; // 이 ACK 페이즈 대상이 아님

    if (it->second)
        return;

	it->second = true;
    waitingAckCount--;

    if (waitingAckCount == 0)
		closed = true;
}

// 한 쪽이 강제종료 or 연결 끊김 시 처리
void Room::OnPlayerDisconnect(const RoomEvent& re)
{
    if (closed.exchange(true))
        return;

    int winnerUserId = -1;
    int loserUserId = -1;

    if (re.sessionId == p1sid) {
        winnerUserId = p2UserId;
        loserUserId = p1UserId;

        EmitOutEvent({ RoomOutEventType::EnemyExit, p1sid });
        EmitOutEvent({ RoomOutEventType::CloseRoom, p1sid });
        SaveRecordAsync(winnerUserId, loserUserId);
    }
    else if (re.sessionId == p2sid) {
        winnerUserId = p1UserId;
        loserUserId = p2UserId;

        EmitOutEvent({ RoomOutEventType::EnemyExit, p2sid });
        EmitOutEvent({ RoomOutEventType::CloseRoom, p2sid });
        SaveRecordAsync(winnerUserId, loserUserId);
    }
    else return;
}

// ThreadPool을 이용해 비동기 전적 저장
void Room::SaveRecordAsync(int winner, int loser)
{
    if (IsBotUser(winner) || IsBotUser(loser))
    {
        std::cout << "[Battle] bot match, skip record\n";
        return;
    }

    SaveRecordRequest req{ winner, loser };

    // Json 직렬화 + API 요청은 비용이 크기 때문에 Thread Pool 사용
    threadPool.Enqueue([req]() mutable {
        try
        {
            nlohmann::json j = req;
            std::string body = j.dump();

            bool success = ApiClient::Instance().Post("battle/save", body);

            if (success)
                std::cout << "[Battle] 전적 저장 성공\n";
            else
                std::cout << "[Battle] 전적 저장 실패\n";
        }
        catch (const std::exception& e)
        {
            std::cerr << "[Battle] SaveRecord exception: " << e.what() << "\n";
        }
        catch (...)
        {
            std::cerr << "[Battle] SaveRecord unknown exception\n";
        }
    });
}

void Room::CloseRoom()
{
    if (closed.exchange(true))
        return;

	ClientSession* s1 = Server::Instance().FindSession(p1sid);
    ClientSession* s2 = Server::Instance().FindSession(p2sid);

    if (s1) s1->SetRoomId(-1);
    if (s2) s2->SetRoomId(-1);
}

