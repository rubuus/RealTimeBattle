#pragma once
#include <chrono>
#include <atomic>
#include <vector>
#include "PacketHeader.h"
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "ws2_32.lib")

constexpr int RECV_BUFFER_SIZE = 8192;

enum class IOType { Recv, Send };

struct OverlappedEx {
    OVERLAPPED ov{};
    IOType type = IOType::Recv;
};

struct SendContext {
    OverlappedEx ovEx{};
    WSABUF wsaBuf{};
    std::vector<char> data; // 길이 가변 패킷도 안전
};

class Room;
struct PacketHeader;
enum class S2C_PacketType : uint16_t;
enum class C2S_PacketType : uint16_t;

class ClientSession {
public:
    ClientSession(SOCKET s, int id);
    ~ClientSession();

    void PostRecv();
    void OnRecv(int bytes);

    void SendPacket(S2C_PacketType type);

    template<typename T>
    void SendPacket(S2C_PacketType type, const T& body)
    {
        static_assert(std::is_trivially_copyable_v<T>,
            "Packet body must be trivially copyable");

        SendPacketInternal(type, &body, sizeof(T));
    }
    
    void Disconnect();

    SOCKET GetSocket() const { return socket; }
    int GetSessionId() const { return sessionId; }

    int GetUserId() const { return userId; }
	void SetUserId(int id) { userId = id; }

	int GetRoomId() const { return roomId; }
	void SetRoomId(int id) { roomId = id; }
    
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

    void HandlePacket(char* packet, int packetSize);

    SOCKET socket = INVALID_SOCKET; // 연결 객체
	Room* room = nullptr; // 소속된 방

    WSABUF recvWsaBuf{};
    OverlappedEx recvOvEx{};

    char recvBuffer[RECV_BUFFER_SIZE];
    int recvBytes = 0;

    int sessionId;
    int userId;
    int roomId;
    std::atomic<bool> disconnected = false;
    bool battleReady = false;
    bool ackReceived = false;
    std::chrono::steady_clock::time_point lastRecvTime = std::chrono::steady_clock::now();
};
