#pragma once
#include <chrono>
#include <atomic>
#include <vector>
#include "PacketHeader.h"
#include "PlayerInputPacket.h"
#include <winsock2.h>
#include <ws2tcpip.h>
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
    std::vector<BYTE> _sendBuffer; // 임시 버퍼
    std::mutex _bufferLock;

public:
    ClientSession(SOCKET s, int id);

    void PostRecv();
    void OnRecv(int bytes);

    template<typename T>
    void SendPacket(S2C_PacketType type, T& packet) {
        // _sendBuffer에 헤더 + 패킷 데이터 memcpy
        // 아직 WSASend 호출 안 함!
    }

    // 2. 실제 전송 (Room::Update 끝에서 호출)
    void FlushSend() {
        if (_sendBuffer.empty()) return;

        // 여기서 WSASend 호출하여 _sendBuffer 내용을 한 방에 전송
        // 전송 후 _sendBuffer 비움
    }

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
	void SetRoom(Room* r) { room = r; }
    
    bool GetReady() const { return battleReady; }
    void SetReady(bool b) { battleReady = b; }

	bool HasInput() const { return hasInput.load(std::memory_order_acquire); }

    PlayerInputPacket ConsumeInput() {
        hasInput.store(false, std::memory_order_release);
        return latestInput;
    }
    
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

    std::atomic<bool> hasInput = false;
    PlayerInputPacket latestInput;

    std::atomic<bool> disconnected = false;
    bool battleReady = false;
    bool ackReceived = false;
    std::chrono::steady_clock::time_point lastRecvTime = std::chrono::steady_clock::now();
};
