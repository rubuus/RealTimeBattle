#pragma once
#include <list>
#include <iostream>
#include <unordered_map>
#include <WinSock2.h>
#include <MSWSock.h>
#include "ThreadPool.h"
#include "Transport.h"

class ClientSession;
class Room;
struct MatchFoundPacket;
enum class Side : uint8_t;

class Server {
public:
	static Server& Instance();
	void StartServer(int port); // Server 시작
	void AcceptLoop();          // accept() 유지
	void WorkerLoop();          // IOCP 워커
	void TickLoop();			// 게임 로직 틱
	void HeartbeatLoop();		// ping 검사
	void StopServer();			// Server 종료

	void AddToMatchList(int sid);
	void CreateRoom(ClientSession* p1, ClientSession* p2);
	void NotifyMatchFound(int roomId, ClientSession* p1, ClientSession* p2);
	void ProcessClosedRooms();
	void CloseRoom(int id);
	void RemoveClient(int sid);

	std::string GetJWTKey() { return secretKey; }

	// Transport에서 꺼내쓰는용
	ClientSession* FindSession(int sid) 
	{ 
		auto it = clients.find(sid);
		if (it == clients.end())
			std::cout << "[FindSession FAIL] sid=" << sid << "\n";
		return it != clients.end() ? it->second.get() : nullptr;
	};

private:
	ThreadPool threadPool{ 4 }; // 내부 로직용 스레드 풀
	std::string secretKey = "V8rG#b3Yp0!tQs7Wk9@Zx2&Nm5eUj4Ha";

	int _port;
	int _roomIdCounter = 1;

	SOCKET listenSocket = INVALID_SOCKET;
	bool running = true;

	HANDLE iocpHandle = nullptr;
	std::vector<std::thread> workers;
	std::vector<std::thread> threads;

	Transport net;

	std::atomic<int> nextClientId{ 1 };

	std::unordered_map<int, std::unique_ptr<ClientSession>> clients;
	std::mutex clientsMutex;

	std::unordered_map<int, std::unique_ptr<Room>> rooms;
	std::mutex roomsMutex;

	std::queue<int> closedRoom;
	std::mutex closedRoomMutex;

	std::list<int> matchList;
	std::mutex matchMutex;
};

