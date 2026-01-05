#pragma once
#include <utility>
#include <memory>
#include "PlayerInputPacket.h"

class Server;
class ClientSession;
class ServerPlayer;

struct TimeSyncPacket;
struct SaveRecordRequest;
struct PlayerInputPacket;

struct Hitbox
{
    float x;
    float y;
    float halfW;
    float halfH;
};

struct Hurtbox
{
    float x;
    float y;
    float halfW;
    float halfH;
};

class Room {
public:
    Room(int id, ClientSession* player1, ClientSession* player2, ThreadPool& pool);
    ~Room();

    void CheckMatch(ClientSession& sender);
	void Update(float dt);
	void OnInputPacket(ClientSession& sender, PlayerInputPacket& p);
	bool IsInPunchRange(ServerPlayer& attacker, ServerPlayer& target);
    bool Overlap(Hitbox& hit, Hurtbox& hurt);
	void CheckDamage();
    void EndGame(); 
    void SendStatePacket();
    void SendDamagePacket();
    void SendTimePacket();
    void SendGameResult();
    void OnAckReceived(ClientSession& s);
    void SaveRecordAsync(ClientSession* winner, ClientSession* loser);
    void OnPlayerDisconnect(ClientSession* s);
	void CloseRoom();
    TimeSyncPacket TimeSync();

private:
    ThreadPool& threadPool;

    int roomId;
    ClientSession* p1;
    ClientSession* p2;
    std::unique_ptr<ServerPlayer> sp1;
    std::unique_ptr<ServerPlayer> sp2;

    std::pair<float, float> leftSpawn;
    std::pair<float, float> rightSpawn;

    bool p1Ready = false;
    bool p2Ready = false;
    bool gameStarted = false;
    int waitingAckCount = 2;
    bool closed = false;
    bool pendingClose = false;
    float gameTime = 100.0f;
    bool startedFirstFrameSent = false;
};