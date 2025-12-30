#pragma once
#include <WinSock2.h>
#include <chrono>
#include <atomic>
#include "PacketHeader.h"

class Room;
struct PacketHeader;
enum class S2C_PacketType;
enum class C2S_PacketType;

class ClientSession {
public:
    ClientSession(SOCKET s, int id);

    

    void PostRecv();
    void OnRecv(int bytesTransferred);

    void SendPacket(S2C_PacketType type);

    template<typename T>
    void SendPacket(S2C_PacketType type, const T& body)
    {
        PacketHeader header;
        header.type = static_cast<uint16_t>(type);
        header.size = sizeof(PacketHeader) + sizeof(T);

        std::vector<char> buf(header.size);
        memcpy(buf.data(), &header, sizeof(header));
        memcpy(buf.data() + sizeof(header), &body, sizeof(T));

        Send(buf.data(), buf.size());
    }
    
    void Disconnect();
    void OnPong();

    int GetUserId() const { return userId; }
	void SetUserId(int id) { userId = id; }

	int GetRoomId() const { return roomId; }
	void SetRoomId(int id) { roomId = id; }
    
    bool GetReady() const { return battleReady; }
    bool SetReady(bool b) { battleReady = b; }
    
	bool IsDisconnected() const { return disconnected.load(); }
	void SetDisconnected(bool b) { disconnected.store(b); }

	bool HasAckReceived() const { return ackReceived; }
	void SetAckReceived(bool b) { ackReceived = b; }


private:
    void Send(const char* data, int len);

    SOCKET socket; // 楷搬 按眉
	Room* room = nullptr; // 家加等 规
    int sessionId;
    int userId;
    int roomId;
    std::atomic<bool> disconnected = false;
    bool battleReady = false;
    bool ackReceived = false;
    std::chrono::steady_clock::time_point lastRecvTime;
};
