#pragma once
#include <queue>
#include <unordered_map>
#include <WinSock2.h>

class ClientSession;
class Room;

class Server {
public:
	Server(int port);

	void StartServer();
	void AddToMatchQueue();
	void CreateRoom();
	void CloseRoom(int id);
	void RemoveClient();

private:
	SOCKET listenSocket;
	int _port;
	int _roomIdCounter = 1;
	std::unordered_map<int, std::unique_ptr<ClientSession>> clients;
	std::unordered_map<int, std::unique_ptr<Room>> rooms;
	std::queue<std::unique_ptr<ClientSession>> matchQueue;
};

