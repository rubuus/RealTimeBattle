#pragma once
#include <utility>
#include <memory>
#include <unordered_map>

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
    Room(int id, ClientSession* player1, ClientSession* player2, ThreadPool& pool);

    void EnqueueEvent(const RoomEvent& ev);
    void Update(double dt);
    void CloseRoom();

private:
    void EmitOutEvent(const RoomOutEvent& ev);
    void ProcessEvents();
    
    void CheckMatch(const RoomEvent& re);
    void OnInput(const RoomEvent& re);
	void ServerPlayerUpdate();

    void EmitStateUpdate();
	void EmitTimeUpdate();
    void EmitDamageUpdate();

	// 타격 판정 관련
    bool CheckDamage(ServerPlayer& attacker, ServerPlayer& target);
	bool IsInPunchRange(ServerPlayer& attacker, ServerPlayer& target);
    bool Overlap(Hitbox& hit, Hurtbox& hurt);
    
	// 게임 종료 처리
    void EndGame();
    void EmitGameResult();
    void BeginCloseAckPhase();
    void OnAckReceived(const RoomEvent& re);
    void OnPlayerDisconnect(const RoomEvent& re);

    // 전적 저장
    void SaveRecordAsync(ClientSession* winner, ClientSession* loser);
	
private:
    ThreadPool& threadPool;
    std::queue<RoomEvent> eventQueue;
    std::mutex eventMutex;
    std::queue<RoomOutEvent> outEvents;

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

    double gameTime = 100.0f;
    double stateSendAcc = 0.0f;
    double timeSendAcc = 0.0f;

	std::unordered_map<int, bool> ackReceivedMap;
    int waitingAckCount = 2;

    std::atomic<bool> closed{ false };
};