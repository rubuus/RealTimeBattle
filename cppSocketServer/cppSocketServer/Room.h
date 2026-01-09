#pragma once
#include <utility>
#include <memory>
#include "PlayerInputPacket.h"
#include "ConcurrentQueue.h"
#include <iostream>

class Server;
class ClientSession;
class ServerPlayer;

struct TimeSyncPacket;
struct SaveRecordRequest;
struct PlayerInputPacket;

struct Hitbox
{
    float x = 0.0f;
    float y = 0.0f;
    float halfW = 0.0f;
    float halfH = 0.0f;
};

struct Hurtbox
{
    float x = 0.0f;
    float y = 0.0f;
    float halfW = 0.0f;
    float halfH = 0.0f;
};


class Room {
public:
    Room(int id, ClientSession* player1, ClientSession* player2, ThreadPool& pool);
    ~Room();

    void CheckMatch(ClientSession& sender);
	void Update(float dt);
    void BroadcastState();
	void BroadcastTime();
	void BroadcastDamage();

    bool CheckDamage(ServerPlayer& attacker, ServerPlayer& target);
	bool IsInPunchRange(ServerPlayer& attacker, ServerPlayer& target);
    bool Overlap(Hitbox& hit, Hurtbox& hurt);
    void UpdateDamage();
    void EndGame(); 
    void SendStatePacket();
    void SendDamagePacket();
    void SendTimePacket();
    void SendGameResult();
    void OnAckReceived(ClientSession& s);
    void SaveRecordAsync(ClientSession* winner, ClientSession* loser);
    void OnPlayerDisconnect(ClientSession* s);
	void CloseRoom();
	bool IsCloseRequested() const { return closeRequested; }
	bool IsClosed() const { return closed.load(); }
    int Id() const { return roomId; }
    TimeSyncPacket TimeSync();

	bool GetPendingTime() const { return pendingTime; }
	void SetPendingTime(bool b) { pendingTime = b; }

	bool GetPendingState() const { return pendingState; }
	void SetPendingState(bool b) { pendingState = b; }

	bool GetPendingDamage() const { return pendingDamage; }
	void SetPendingDamage(bool b) { pendingDamage = b; }

	bool GetPendingEndGame() const { return pendingEndGame; }
	void SetPendingEndGame(bool b) { pendingEndGame = b; }

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
    std::atomic<bool> closed{ false };
    bool pendingClose = false;
	bool closeRequested = false;
    float gameTime = 100.0f;
	float stateSendAcc = 0.0f;
	float timeSendAcc = 0.0f;
    bool startedFirstFrameSent = false;

	bool pendingTime = false;
    bool pendingState = false;
	bool damageHappened = false;
	bool pendingDamage = false;
	bool pendingEndGame = false;
};