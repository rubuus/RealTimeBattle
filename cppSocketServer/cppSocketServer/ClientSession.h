#pragma once
#include <chrono>
#include <atomic>
#include <vector>
#include "PacketHeader.h"
#include "PlayerInputPacket.h"
#include <winsock2.h>
#include <ws2tcpip.h>
#include "PacketRouter.h"
#pragma comment(lib, "ws2_32.lib")

constexpr int RECV_BUFFER_SIZE = 8192;

enum class IOType { Recv, Send };

struct OverlappedEx {
    OVERLAPPED ov;
    IOType type;
};

struct SendContext {
    OverlappedEx ovEx{};
    WSABUF wsaBuf{};
    std::vector<char> data;
};

class Room;
struct PacketHeader;
struct PlayerInputPacket;
enum class S2C_PacketType : uint16_t;
enum class C2S_PacketType : uint16_t;

class ClientSession {

public:
    ClientSession(SOCKET s, int id);

    void PostRecv();
    void OnRecv(int bytes);

    // 헤더만 있을 경우
    void SendPacket(S2C_PacketType type);

    // 바디 포함 패킷일 경우
    template<typename T>
    void SendPacket(S2C_PacketType type, const T& body)
    {
        SendPacketInternal(type, &body, sizeof(T));
    }

	void OnPacket(const ParsedPacket& pkt);
    
    void Disconnect();

    SOCKET GetSocket() const { return socket; }
    int GetSessionId() const { return sessionId; }

    int GetUserId() const { return userId; }
	void SetUserId(int id) { userId = id; }

	int GetRoomId() const { return roomId; }
    void SetRoomId(int id) { roomId = id; }

	Room* GetRoom() const { return room; }
	void SetRoom(Room* r) { room = r; }
    
    bool GetReady() const { return battleReady; }
    void SetReady(bool b) { battleReady = b; }
    
	bool IsDisconnected() const { return disconnected.load(); }
	void SetDisconnected(bool b) { disconnected.store(b); }

	bool HasAckReceived() const { return ackReceived; }
	void SetAckReceived(bool b) { ackReceived = b; }

	std::chrono::steady_clock::time_point GetLastRecvTime() const { return lastRecvTime; }
    void SetLastRecvTime() { lastRecvTime = std::chrono::steady_clock::now(); }

private:
    void SendPacketInternal(
        S2C_PacketType type,
        const void* body,
        size_t bodySize);

    SOCKET socket = INVALID_SOCKET; // 연결 객체
	Room* room = nullptr; // 소속된 방

    WSABUF recvWsaBuf{};
    OverlappedEx recvOvEx{};

    char recvBuffer[RECV_BUFFER_SIZE];
    int recvBytes = 0;

    int sessionId;
    int userId;
    int roomId;

    std::atomic<bool> hasInput = false;
    PlayerInputPacket latestInput;

    std::atomic<bool> disconnected = false;
    bool battleReady = false;
    bool ackReceived = false;
    std::chrono::steady_clock::time_point lastRecvTime = std::chrono::steady_clock::now();
};
