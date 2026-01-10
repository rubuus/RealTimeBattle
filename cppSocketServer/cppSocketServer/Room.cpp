#include <memory>
#include <cmath>
#include <iostream>
#include <nlohmann/json.hpp>
#include "ClientSession.h"
#include "Room.h"
#include "ServerPlayer.h"
#include "SaveRecordRequest.h"
#include "ApiClient.h"
#include "MatchFoundPacket.h"

Room::Room(int id, ClientSession* player1, ClientSession* player2, ThreadPool& pool)
    : roomId(id), p1(player1), p2(player2), threadPool(pool)
{
    leftSpawn = { -7.0f, -2.3f };
    rightSpawn = { 7.0f, -2.3f };

    sp1 = std::make_unique<ServerPlayer>(
        p1->GetUserId(),
        static_cast<int8_t>(Side::Left),
        leftSpawn
    );

    sp2 = std::make_unique<ServerPlayer>(
        p2->GetUserId(),
        static_cast<int8_t>(Side::Right),
        rightSpawn
    );
}

// Network/IO 스레드 → Room 스레드로 전달되는 이벤트 enqueue
void Room::EnqueueEvent(const RoomEvent& ev)
{
    std::lock_guard<std::mutex> lock(eventMutex);
    eventQueue.push(ev);
}

// Network/IO 스레드로 전달되는 이벤트 enqueue
void Room::EmitOutEvent(const RoomOutEvent& ev)
{
    outEvents.push(ev);
}

// 이벤트 큐 처리 (룸 업데이트 스레드에서 호출)
void Room::ProcessEvents()
{
    while (!eventQueue.empty())
    {
        auto ev = eventQueue.front();
        eventQueue.pop();

        switch (ev.type)
        {
            case RoomEventType::BattleReady:
                CheckMatch(ev);
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

void Room::Update(double dt)
{
    if (!gameStarted) return;
    if (!p1 || !p2) return;

    gameTime -= dt;

    ServerPlayerUpdate();

    stateSendAcc += dt;
    if (stateSendAcc >= 0.033f)
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
	auto p1sid = p1->GetSessionId();
    auto p2sid = p2->GetSessionId();

    if (re.sessionId == p1sid)
        p1Ready = true;

    if (re.sessionId == p2sid)
        p2Ready = true;

    if (p1Ready && p2Ready)
    {
        gameStarted = true;

        EmitOutEvent({ RoomOutEventType::LoadBattle, p1sid });
        EmitOutEvent({ RoomOutEventType::LoadBattle, p2sid });
    }
}

// Player로직에 Input 값 넘겨주기
void Room::OnInput(const RoomEvent& re)
{
    if (re.sessionId == p1->GetSessionId())
        sp1->ApplyInput(re.input);
    else if (re.sessionId == p2->GetSessionId())
        sp2->ApplyInput(re.input);
}

void Room::ServerPlayerUpdate() 
{
    sp1->Update();
    sp2->Update();
}

void Room::EmitStateUpdate()
{
	stateSendAcc = 0.0f;

    int p1sid = p1->GetSessionId();
    int p2sid = p2->GetSessionId();

    auto s1 = sp1->StatePacket();
    auto s2 = sp2->StatePacket();

    EmitOutEvent({ RoomOutEventType::StateUpdate, p1sid, UpdateStatePayload{ s1, s2 } });
    EmitOutEvent({ RoomOutEventType::StateUpdate, p2sid, UpdateStatePayload{ s1, s2 } });
}

void Room::EmitTimeUpdate()
{
    timeSendAcc = 0.0f;

    int p1sid = p1->GetSessionId();
    int p2sid = p2->GetSessionId();

    auto t = static_cast<int32_t>(std::ceil(gameTime));

    EmitOutEvent({ RoomOutEventType::TimeUpdate, p1sid, UpdateTimePayload {t} });
    EmitOutEvent({ RoomOutEventType::TimeUpdate, p2sid, UpdateTimePayload {t} });
}

void Room::EmitDamageUpdate() {

    int p1sid = p1->GetSessionId();
    int p2sid = p2->GetSessionId();

    auto d1 = sp1->HurtPacket();
    auto d2 = sp2->HurtPacket();

    if (CheckDamage(*sp1, *sp2)) 
    {
        EmitOutEvent({ RoomOutEventType::Attack, p1sid, UpdateHurtPayload { d2 } });
        EmitOutEvent({ RoomOutEventType::Attack, p2sid, UpdateHurtPayload { d2 } });
    }
    else if (CheckDamage(*sp2, *sp1))
    {
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
    if (attacker.GetPosition().first > target.GetPosition().first && target.GetDir() < 0)
        target.SetDir(1);
    else if (attacker.GetPosition().first < target.GetPosition().first && target.GetDir() > 0)
        target.SetDir(-1);

    target.TakeDamage(10, attacker.GetDir() * 1.0f);

    return true;
}

// hitbox와 hurtbox 위치 및 크기 생성
bool Room::IsInPunchRange(ServerPlayer& attacker, ServerPlayer& target)
{
    Hitbox hitbox;
    Hurtbox hurtbox;

    hitbox.x = attacker.GetPosition().first + (0.3f * attacker.GetDir());
    hitbox.y = attacker.GetPosition().second;
    hitbox.halfW = 0.3f;
    hitbox.halfH = 0.7f;

    hurtbox.x = target.GetPosition().first;
    hurtbox.y = target.GetPosition().second;
    hurtbox.halfW = 0.5f;
    hurtbox.halfH = 1.0f;

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

void Room::EndGame()
{
    if (closed) return;

    EmitGameResult();
    BeginCloseAckPhase();
}

void Room::EmitGameResult()
{
    int p1sid = p1->GetSessionId();
    int p2sid = p2->GetSessionId();

    int p1hp = sp1->GetCurrentHP();
    int p2hp = sp2->GetCurrentHP();

    if (p1hp > p2hp)
    {
        EmitOutEvent({ RoomOutEventType::GameResult, p1sid, GameResultPayload { p1sid } });
        EmitOutEvent({ RoomOutEventType::GameResult, p2sid, GameResultPayload { p1sid } });
        SaveRecordAsync(p1, p2);
    }
    else if (p1hp < p2hp)
    {
        EmitOutEvent({ RoomOutEventType::GameResult, p1sid, GameResultPayload { p2sid } });
        EmitOutEvent({ RoomOutEventType::GameResult, p2sid, GameResultPayload { p2sid } });
        SaveRecordAsync(p2, p1);
    }
    else
    {
        EmitOutEvent({ RoomOutEventType::GameResult, p1sid, GameResultPayload { -1 } });
        EmitOutEvent({ RoomOutEventType::GameResult, p2sid, GameResultPayload { -1 } });
        SaveRecordAsync(p1, p2);
    }
}

// 게임 종료 후 ACK 패킷 대기 단계 시작
void Room::BeginCloseAckPhase()
{
    int p1sid = p1->GetSessionId();
    int p2sid = p2->GetSessionId();

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

    ClientSession* winner = nullptr;
    ClientSession* loser = nullptr;

    if (re.sessionId == p1->GetSessionId()) {
        winner = p2;
        loser = p1;
    }
    else if (re.sessionId == p2->GetSessionId()) {
        winner = p1;
        loser = p2;
    }
    else return;

    EmitOutEvent({ RoomOutEventType::EnemyExit, winner->GetSessionId() });
    EmitOutEvent({ RoomOutEventType::CloseRoom, winner->GetSessionId() });
    SaveRecordAsync(winner, loser);
}

// ThreadPool을 이용해 비동기 전적 저장
void Room::SaveRecordAsync(ClientSession* winner, ClientSession* loser)
{
    SaveRecordRequest req{ winner->GetUserId(), loser->GetUserId() };

    threadPool.Enqueue([req]() mutable {
        nlohmann::json j = req;
        std::string body = j.dump();

        bool success = ApiClient::Instance().Post("battle/save", body);

        if (success)
            std::cout << "[Battle] 전적 저장 성공\n";
        else
            std::cout << "[Battle] 전적 저장 실패\n";
    });
}

void Room::CloseRoom()
{
    if (closed) return;

    if (p1) p1->SetRoom(nullptr);
    if (p2) p2->SetRoom(nullptr);
}

