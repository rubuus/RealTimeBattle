#pragma once
#include <chrono>
#include <atomic>
#include <vector>
#include <winsock2.h>
#include <ws2tcpip.h>
#include "PlayerInputPacket.h"
#include "PacketRouter.h"
#pragma comment(lib, "ws2_32.lib")

class Room;
class PacketRouter;
struct PacketHeader;
struct ParsedPacket;
struct PlayerInputPacket;
enum class S2C_HeaderType : uint16_t;
enum class C2S_HeaderType : uint16_t;

constexpr int RECV_BUFFER_SIZE = 8192;

enum class IOType { Recv, Send };

struct OverlappedEx {
    OVERLAPPED ov;
    IOType type;
};

struct RecvContext {
    OverlappedEx ovEx{};
    WSABUF wsaBuf{};
    std::vector<char> data;
};

struct SendContext {
    OverlappedEx ovEx{};
    WSABUF wsaBuf{};
    std::vector<char> data;
};

class ClientSession {

public:
    ClientSession(SOCKET s, int id);

    bool GetAuthenticated() { return isAuthenticated; }
    void SetAuthenticated(bool b) { isAuthenticated = b; }

    void PostRecv();
    void OnRecv(DWORD bytes);
    void OnSend(DWORD bytes);

    // 헤더만 있을 경우
    void SendPacket(S2C_HeaderType type);

    // 바디 포함 패킷일 경우
    template<typename T>
    void SendPacket(S2C_HeaderType type, const T& body)
    {
        SendPacketInternal(type, &body, sizeof(T));
    }

	void OnPacket(const ParsedPacket& pkt);
    
    void Disconnect(const char* why);

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

    // IO 추가
    void AddIo()
    {
        pendingIo.fetch_add(1, std::memory_order_relaxed);
    }

    // 처리 완료 됐으면 빼기
    void ReleaseIo()
    {
        pendingIo.fetch_sub(1, std::memory_order_relaxed);
    }

    // 연결이 끊겼고, 모든 IO가 완료되면, 세션을 안전하게 정리할 수 있는지 확인
    bool CanCleanup() const
    {
        return disconnected.load(std::memory_order_acquire)
            && pendingIo.load(std::memory_order_acquire) == 0;
    }

	std::chrono::steady_clock::time_point GetLastRecvTime() const { return lastRecvTime; }
    void SetLastRecvTime() { lastRecvTime = std::chrono::steady_clock::now(); }

private:
    void SendPacketInternal(
        S2C_HeaderType type,
        const void* body,
        size_t bodySize);

    SOCKET socket = INVALID_SOCKET; // 연결 객체
	Room* room = nullptr; // 소속된 방

    bool isAuthenticated = false;

    char recvBuffer[RECV_BUFFER_SIZE];
    int32_t recvBytes;

    int32_t sessionId;
    int32_t userId;
    int32_t roomId;

    std::atomic<bool> hasInput = false;
    PlayerInputPacket latestInput;

    std::atomic<int>  pendingIo = 0;     // 진행 중 IO 개수
    std::atomic<bool> disconnected = false;

    bool battleReady = false;
    bool ackReceived = false;

    std::chrono::steady_clock::time_point lastRecvTime;
};
