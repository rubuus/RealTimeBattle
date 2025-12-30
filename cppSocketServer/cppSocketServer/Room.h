#pragma once
#include <utility>
#include <memory>

class Server;
class ClientSession;
class ServerPlayer;

struct TimeSyncPacket;
struct SaveRecordRequest;

class Room {
public:
    Room(int id, ClientSession& player1, ClientSession& player2);
	
	void Update();
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
    void SaveRecordAsync(ClientSession& winner, ClientSession& loser);
    void OnPlayerDisconnect(ClientSession& s);
	void CloseRoom();

private:
    void CheckMatch(ClientSession& sender);
    TimeSyncPacket TimeSync();

    int roomId;
    Server* server;
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