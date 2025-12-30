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

Room::Room(int id, ClientSession& player1, ClientSession& player2)
    : roomId(id), p1(&player1), p2(&player2)
{
    leftSpawn = { -7.0f, -2.3f };
    rightSpawn = { 7.0f, -2.3f };

    sp1 = std::make_unique<ServerPlayer>(
        p1->GetUserId(),
        "LEFT",
        leftSpawn
    );

    sp2 = std::make_unique<ServerPlayer>(
        p2->GetUserId(),
        "RIGHT",
        rightSpawn
    );
}


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
        server->CloseRoom(this->roomId);
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
    p1->SendPacket(S2C_PacketType::PLAYER_STATE, sp1->StatePacket());
    p1->SendPacket(S2C_PacketType::PLAYER_STATE, sp2->StatePacket());
    p2->SendPacket(S2C_PacketType::PLAYER_STATE, sp1->StatePacket());
    p2->SendPacket(S2C_PacketType::PLAYER_STATE, sp2->StatePacket());
}

void Room::SendDamagePacket()
{
    p1->SendPacket(S2C_PacketType::TAKE_DAMAGE, sp1->HurtPacket());
    p1->SendPacket(S2C_PacketType::TAKE_DAMAGE, sp2->HurtPacket());
    p2->SendPacket(S2C_PacketType::TAKE_DAMAGE, sp1->HurtPacket());
    p2->SendPacket(S2C_PacketType::TAKE_DAMAGE, sp2->HurtPacket());
}

void Room::SendTimePacket()
{
    p1->SendPacket(S2C_PacketType::GAME_TIME, TimeSync());
    p2->SendPacket(S2C_PacketType::GAME_TIME, TimeSync());
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
        _ = SaveRecordAsync(p1, p2);
    }
    else if (sp1->GetCurrentHP() < sp2->GetCurrentHP())
    {
        p1->SendPacket(S2C_PacketType::GAME_LOSE);
        p2->SendPacket(S2C_PacketType::GAME_WIN);
        _ = SaveRecordAsync(p2, p1);
    }
    else
    {
        p1->SendPacket(S2C_PacketType::GAME_DRAW);
        p2->SendPacket(S2C_PacketType::GAME_DRAW);
        _ = SaveRecordAsync(p1, p2);
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

void Room::SaveRecordAsync(ClientSession& winner, ClientSession& loser)
{
    auto req = SaveRecordRequest
    {
        winner.GetUserId(),
        loser.GetUserId()
    };

    bool success = await ApiClient.Post("battle/save", req);

    if (success)
		std::cout << "전적 저장 성공\n";
    else
		std::cout << "전적 저장 실패\n";
}

void Room::EndGame()
{
    if (closed) return;
    closed = true;

    SendGameResult();

    waitingAckCount = 2;
}

void Room::OnPlayerDisconnect(ClientSession& s)
{
    if (closed) return;
    closed = true;

    ClientSession* winner = nullptr;
    ClientSession* loser = nullptr;

    if (&s == p1) {
        winner = p2;
        loser = p1;
    }
    else if (&s == p2) {
        winner = p1;
        loser = p2;
    }

    winner->SendPacket(S2C_PacketType::ENEMY_EXIT);
    winner->SendPacket(S2C_PacketType::ROOM_CLOSED);
    _ = SaveRecordAsync(winner, loser);

    SocketServer.Instance.CloseRoom(roomId);
}

void Room::CloseRoom()
{
    if (closed) return;
    closed = true;

	p1->SetRoomId(-1);
    p2->SetRoomId(-1);
}

