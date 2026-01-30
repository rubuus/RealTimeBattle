#pragma once
#include <utility>
#include <memory>
#include <queue>
#include <unordered_map>
#include <mutex>
#include "RoomEvent.h"
#include "ServerPlayer.h"
#include "Transport.h"
#include <unordered_set>

class ClientSession;
class ServerPlayer;
class ThreadPool;

struct RoomEvent;
struct RoomOutEvent;
struct SaveRecordRequest;

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
    Room(int id, int player1, int player2, ThreadPool& pool);

    int GetRoomId() { return roomId; }
    void EnqueueEvent(const RoomEvent& ev);
    void Update(double dt);
    void CloseRoom();
    bool IsClosed() const { return closed.load(); }

	int GetP1UserId() const { return p1UserId; }
	void SetP1UserId(int id) { p1UserId = id; }

	int GetP2UserId() const { return p2UserId; }
	void SetP2UserId(int id) { p2UserId = id; }

private:
    void EmitOutEvent(const RoomOutEvent& ev);
    void InputEvents();
    void OutEvents();
    
    void CheckMatch(const RoomEvent& re);
    void PlayerSpawn(const RoomEvent& re);
    void OnInput(const RoomEvent& re);
	void ServerPlayerUpdate(double dt);

    void EmitStateUpdate();
	void EmitTimeUpdate();
    void EmitDamageUpdate();

	// 타격 판정 관련
    bool CheckDamage(ServerPlayer& attacker, ServerPlayer& target);
	bool IsInPunchRange(ServerPlayer& attacker, ServerPlayer& target);
    bool Overlap(Hitbox& hit, Hurtbox& hurt);
    
	// 게임 종료 처리
    bool IsBotUser(int sid);
    void EndGame();
    void EmitGameResult();
    void BeginCloseAckPhase();
    void OnAckReceived(const RoomEvent& re);
    void OnPlayerDisconnect(const RoomEvent& re);

    // 전적 저장
    void SaveRecordAsync(int winner, int loser);
	
private:
    ThreadPool& threadPool;
    std::queue<RoomEvent> eventQueue;
    std::mutex eventMutex;
    std::queue<RoomOutEvent> outEventQueue;
    std::mutex outEventMutex;
    std::chrono::steady_clock::time_point lastLog;
    Transport net;

    int roomId;
	int p1UserId;
    int p2UserId;
    int p1sid;
    int p2sid;
    std::unique_ptr<ServerPlayer> sp1;
    std::unique_ptr<ServerPlayer> sp2;
    std::chrono::steady_clock::time_point lastUpdate;
    std::chrono::steady_clock::time_point lastLagLog;
    Vector2 leftSpawn;
    Vector2 rightSpawn;

    std::unordered_set<int> ready;
    bool gameStarted = false;
    bool battleStarted = false;

    double gameTime = 100.0f;
    double stateSendAcc = 0.0f;
    double timeSendAcc = 0.0f;

	std::unordered_map<int, bool> ackReceivedMap;
    int waitingAckCount = 2;

    std::atomic<bool> closed{ false };
};