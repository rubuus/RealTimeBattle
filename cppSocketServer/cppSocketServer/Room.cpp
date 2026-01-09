#include "Server.h"
#include "ClientSession.h"
#include "Room.h"
#include "ServerPlayer.h"
#include "PacketHeader.h"
#include "TimeSyncPacket.h"
#include "SaveRecordRequest.h"
#include <memory>
#include <cmath>
#include <iostream>
#include <nlohmann/json.hpp>
#include "ApiClient.h"
#include "MatchFoundPacket.h"
#include "ServerPlayer.h"
#include <numbers>

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

Room::~Room() = default;


void Room::CheckMatch(ClientSession& sender)
{
    if (sender.GetUserId() == p1->GetUserId())
        p1Ready = true;

    if (sender.GetUserId() == p2->GetUserId())
        p2Ready = true;

    if (p1Ready && p2Ready)
    {
        gameStarted = true;
        p1->SendPacket(S2C_PacketType::LOAD_BATTLE);
        p2->SendPacket(S2C_PacketType::LOAD_BATTLE);
    }
}

void Room::Update(float dt)
{
    if (!gameStarted) return;
    if (!p1 || !p2) return; // 안전장치

    gameTime -= dt;

    // 1. 입력 처리 및 물리 시뮬레이션
    if (p1->HasInput()) sp1->ApplyInput(p1->ConsumeInput());
    if (p2->HasInput()) sp2->ApplyInput(p2->ConsumeInput());

    sp1->Update();
    sp2->Update();

    // 2. 데미지 판정 (판정 즉시 패킷 버퍼링)
    UpdateDamage();

    // 3. 상태 패킷 버퍼링 (State Sync)
    // 60Hz 서버라면 매 프레임 보내는 것이 가장 부드럽습니다.
    // 대역폭이 걱정된다면 dt를 누적해서 30Hz(0.033f)마다 보내세요.
    stateSendAcc += dt;
    if (stateSendAcc >= 0.033f) // 약 30Hz 전송 (PC게임이면 0으로 설정해 매번 전송 권장)
    {
        stateSendAcc = 0.0f;
        BroadcastState(); // [변경] 플래그 세팅 대신 즉시 버퍼에 씀
    }

    // 4. 시간 동기화 (자주 보낼 필요 없음, 1초에 1번이면 충분)
    timeSendAcc += dt;
    if (timeSendAcc >= 1.0f)
    {
        timeSendAcc = 0.0f;
        BroadcastTime();
    }

    // 5. 게임 종료 체크
    if (gameTime <= 0.0f || sp1->GetCurrentHP() <= 0 || sp2->GetCurrentHP() <= 0)
    {
        EndGame();
    }

    // [핵심] 이번 프레임에 발생한 모든 패킷(이동, 타격, 시간 등)을 
    // 한 번의 TCP 패킷으로 뭉쳐서 OS에 전송 요청 (Flush)
    // ClientSession에 FlushSend() 기능이 구현되어 있어야 합니다.
    p1->FlushSend();
    p2->FlushSend();
}

// [변경] 리턴값 없이 내부에서 바로 패킷을 버퍼에 씁니다.
void Room::UpdateDamage()
{
    bool p1Hit = CheckDamage(*sp1, *sp2); // p1이 p2를 때림
    bool p2Hit = CheckDamage(*sp2, *sp1); // p2가 p1을 때림

    if (p1Hit || p2Hit)
    {
        BroadcastDamage(); // 데미지가 발생했을 때만 패킷 생성
    }
}

// [변경] Send -> Broadcast (이름 변경: '버퍼에 쓴다'는 의미 강조)
void Room::BroadcastState()
{
    // 패킷 생성 비용을 아끼기 위해 미리 만듭니다.
    auto s1 = sp1->StatePacket();
    auto s2 = sp2->StatePacket();

    // SendPacket은 소켓에 바로 쏘지 않고, Session의 _sendBuffer에 memcpy만 해야 함
    p1->SendPacket(S2C_PacketType::PLAYER_STATE, s1);
    p1->SendPacket(S2C_PacketType::PLAYER_STATE, s2);

    p2->SendPacket(S2C_PacketType::PLAYER_STATE, s1);
    p2->SendPacket(S2C_PacketType::PLAYER_STATE, s2);
}

void Room::BroadcastDamage()
{
    auto d1 = sp1->HurtPacket();
    auto d2 = sp2->HurtPacket();

    p1->SendPacket(S2C_PacketType::TAKE_DAMAGE, d1);
    p1->SendPacket(S2C_PacketType::TAKE_DAMAGE, d2);

    p2->SendPacket(S2C_PacketType::TAKE_DAMAGE, d1);
    p2->SendPacket(S2C_PacketType::TAKE_DAMAGE, d2);
}

void Room::BroadcastTime()
{
    auto timePacket = TimeSync();
    p1->SendPacket(S2C_PacketType::GAME_TIME, timePacket);
    p2->SendPacket(S2C_PacketType::GAME_TIME, timePacket);
}

bool Room::CheckDamage(ServerPlayer& attacker, ServerPlayer& target)
{
    if (attacker.GetState() != PlayerState::Punch)
        return false;

    if (attacker.HasPunchChecked())
        return false;

    attacker.SetPunchChecked(true);

    if (!IsInPunchRange(attacker, target))
        return false;

    if (attacker.GetPosition().first > target.GetPosition().first && target.GetDir() < 0)
        target.SetDir(1);
    else if (attacker.GetPosition().first < target.GetPosition().first && target.GetDir() > 0)
        target.SetDir(-1);

    target.TakeDamage(10, attacker.GetDir() * 1.0f);

    return true;
}

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

bool Room::Overlap(Hitbox& hit, Hurtbox& hurt)
{
    float diffX = std::abs(hit.x - hurt.x);
    float diffY = std::abs(hit.y - hurt.y);
    float limitX = hit.halfW + hurt.halfW;
    float limitY = hit.halfH + hurt.halfH;

    return (diffX <= limitX) && (diffY <= limitY);
}

void Room::UpdateDamage() {
    damageHappened = false;

    if (CheckDamage(*sp1, *sp2))
        damageHappened = true;
    else if (CheckDamage(*sp2, *sp1))
        damageHappened = true;
}

void Room::SendStatePacket()
{
    auto s1 = sp1->StatePacket();
    auto s2 = sp2->StatePacket();

    p1->SendPacket(S2C_PacketType::PLAYER_STATE, s1);
    p1->SendPacket(S2C_PacketType::PLAYER_STATE, s2);
    p2->SendPacket(S2C_PacketType::PLAYER_STATE, s1);
    p2->SendPacket(S2C_PacketType::PLAYER_STATE, s2);
}

void Room::SendDamagePacket()
{
    auto s1 = sp1->HurtPacket();
    auto s2 = sp2->HurtPacket();

    p1->SendPacket(S2C_PacketType::TAKE_DAMAGE, s1);
    p1->SendPacket(S2C_PacketType::TAKE_DAMAGE, s2);
    p2->SendPacket(S2C_PacketType::TAKE_DAMAGE, s1);
    p2->SendPacket(S2C_PacketType::TAKE_DAMAGE, s2);
}

void Room::SendTimePacket()
{
	auto timePacket = TimeSync();

    p1->SendPacket(S2C_PacketType::GAME_TIME, timePacket);
    p2->SendPacket(S2C_PacketType::GAME_TIME, timePacket);
}

TimeSyncPacket Room::TimeSync() {
    return TimeSyncPacket {
        static_cast<int32_t>(std::ceil(gameTime))
    };
}

void Room::SendGameResult()
{
    if (sp1->GetCurrentHP() > sp2->GetCurrentHP())
    {
        p1->SendPacket(S2C_PacketType::GAME_WIN);
        p2->SendPacket(S2C_PacketType::GAME_LOSE);
        SaveRecordAsync(p1, p2);
    }
    else if (sp1->GetCurrentHP() < sp2->GetCurrentHP())
    {
        p1->SendPacket(S2C_PacketType::GAME_LOSE);
        p2->SendPacket(S2C_PacketType::GAME_WIN);
        SaveRecordAsync(p2, p1);
    }
    else
    {
        p1->SendPacket(S2C_PacketType::GAME_DRAW);
        p2->SendPacket(S2C_PacketType::GAME_DRAW);
        SaveRecordAsync(p1, p2);
    }
}

void Room::OnAckReceived(ClientSession& s)
{
    if (s.HasAckReceived()) return;
    s.SetAckReceived(true);

    waitingAckCount--;

    if (waitingAckCount == 0)
    {
        p1->SendPacket(S2C_PacketType::ROOM_CLOSED);
        p2->SendPacket(S2C_PacketType::ROOM_CLOSED);
        pendingClose = true;
    }
}

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

void Room::EndGame()
{
    if (closed) return;
    closed = true;

    SendGameResult();

    waitingAckCount = 2;
}

void Room::OnPlayerDisconnect(ClientSession* s)
{
    if (closed) return;
    closed = true;

    ClientSession* winner = nullptr;
    ClientSession* loser = nullptr;

    if (s == p1) {
        winner = p2;
        loser = p1;
    }
    else if (s == p2) {
        winner = p1;
        loser = p2;
    }

    winner->SendPacket(S2C_PacketType::ENEMY_EXIT);
    winner->SendPacket(S2C_PacketType::ROOM_CLOSED);
    SaveRecordAsync(winner, loser);

    closeRequested = true;
}

void Room::CloseRoom()
{
    // 이미 닫혔으면 즉시 리턴
    if (closed.exchange(true))
        return;

    if (p1) p1->SetRoom(nullptr);
    if (p2) p2->SetRoom(nullptr);
}

