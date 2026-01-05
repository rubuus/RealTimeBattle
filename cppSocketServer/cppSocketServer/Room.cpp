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
    if (!p1->GetReady() || !p2->GetReady()) return;
    if (!startedFirstFrameSent)
    {
        startedFirstFrameSent = true;
        return;
    }

    gameTime -= dt;
    SendTimePacket();

    if (gameTime <= 0.0f || sp1->GetCurrentHP() <= 0 || sp2->GetCurrentHP() <= 0)
    {
        p1->SetReady(false);
        p2->SetReady(false);
        EndGame();
        return;
    }

    sp1->Update(dt);
    sp2->Update(dt);

    CheckDamage();
    SendStatePacket();

    if (pendingClose)
    {
        pendingClose = false;
        Server::Instance().CloseRoom(this->roomId);
    }
}

void Room::OnInputPacket(ClientSession& sender, PlayerInputPacket& p)
{
    if (sender.GetUserId() == p1->GetUserId())
        sp1->ApplyInput(p);

    else
        sp2->ApplyInput(p);
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

    attacker.SetPunchPressed(false);

    return Overlap(hitbox, hurtbox);
}

bool Room::Overlap(Hitbox& hit, Hurtbox& hurt)
{
    return !(abs(hit.x - hurt.x) > (hit.halfW + hurt.halfW) ||
        abs(hit.y - hurt.y) > (hit.halfH + hurt.halfH));
}

void Room::CheckDamage()
{
    // player1 -> player2 공격
    if (sp1->GetState() == PlayerState::Punch && sp1->GetPunchPressed())
    {
        if (IsInPunchRange(*sp1, *sp2))
        {
            if (sp1->GetPosition().first > sp2->GetPosition().first && sp2->GetDir() < 0)
                sp2->SetDir(1);
            else if (sp1->GetPosition().first < sp2->GetPosition().first && sp2->GetDir() > 0)
                sp2->SetDir(-1);

            sp2->TakeDamage(10, sp1->GetDir() * 1.0f);
            SendDamagePacket();
        }
    }
    // player2 -> player1 공격
    else if (sp2->GetState() == PlayerState::Punch && sp2->GetPunchPressed())
    {
        if (sp2->GetPosition().first > sp1->GetPosition().first && sp1->GetDir() < 0)
            sp1->SetDir(1);
        else if (sp2->GetPosition().first < sp1->GetPosition().first && sp1->GetDir() > 0)
            sp1->SetDir(-1);

        sp1->TakeDamage(10, sp2->GetDir() * 1.0f);
        SendDamagePacket();
    }
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

    nlohmann::json j = req;
    std::string body = j.dump();

    threadPool.Enqueue([body]() mutable {

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

    Server::Instance().CloseRoom(roomId);
}

void Room::CloseRoom()
{
    if (closed) return;
    closed = true;

	p1->SetRoomId(-1);
    p2->SetRoomId(-1);
}

