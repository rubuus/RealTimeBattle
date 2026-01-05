#pragma once
#include <queue>
#include <unordered_map>
#include <WinSock2.h>
#include "ThreadPool.h"
#include <MSWSock.h>

class ClientSession;
class Room;
struct MatchFoundPacket;
enum class Side : uint8_t;

class Server {
public:
	static Server& Instance();
	void StartServer(int port);
	void AcceptLoop();          // accept() 유지
	void WorkerLoop();          // IOCP 워커

	void AddToMatchQueue(int sid);
	void CreateRoom(ClientSession* p1, ClientSession* p2);
	void CloseRoom(int id);
	void RemoveClient(ClientSession* s);

	void TickLoop();
	void HeartbeatLoop();
	void CheckHeartbeat();

private:
	ThreadPool threadPool{ 4 };
	int _port;
	int _roomIdCounter = 1;

	SOCKET listenSocket = INVALID_SOCKET;

	HANDLE iocpHandle = nullptr;
	std::vector<std::thread> workers;

	std::unordered_map<int, std::unique_ptr<ClientSession>> clients;
	std::mutex clientsMutex;

	std::unordered_map<int, std::unique_ptr<Room>> rooms;
	std::mutex roomsMutex;

	std::queue<int> matchQueue;
	std::mutex matchMutex;
};

