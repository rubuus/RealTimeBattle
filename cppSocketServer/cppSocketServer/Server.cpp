#include "Server.h"
#include "ClientSession.h"
#include "Room.h"
#include <iostream>
#include <mswsock.h>
#include <chrono>
#pragma comment(lib, "ws2_32.lib")

Server::Server(int port)
	: _port(port)
{
}

void InitAcceptEx(SOCKET listenSock)
{
    GUID guidAcceptEx = WSAID_ACCEPTEX;
    DWORD bytes = 0;
    int result = WSAIoctl(
        listenSock,
        SIO_GET_EXTENSION_FUNCTION_POINTER,
        &guidAcceptEx,
        sizeof(guidAcceptEx),
        &lpfnAcceptEx,
        sizeof(lpfnAcceptEx),
        &bytes,
        nullptr,
        nullptr
    );
    if (result == SOCKET_ERROR || lpfnAcceptEx == nullptr)
    {
        std::cerr << "Failed to get AcceptEx pointer: " << WSAGetLastError() << std::endl;
        exit(1);
    }
}

void Server::StartServer() {
    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);

    listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_port = htons(_port);
    addr.sin_addr.s_addr = INADDR_ANY;

    bind(listenSocket, (sockaddr*)&addr, sizeof(addr));
    listen(listenSocket, SOMAXCONN);

    InitAcceptEx(listenSocket);

	std::cout << "Server Started\n";

    AcceptLoop();
}

void AcceptLoop() {
    int clientId = 1;

    while (true) {
        SOCKET clientSock = accept(listenSocket, nullptr, nullptr);
        if (clientSock == INVALID_SOCKET)
            continue;

        auto session = std::make_unique<ClientSession>(clientId, clientSock);

        clients[clientId] = std::move(session);
        clientId++;
    }
}

void PostAccept() {
    SOCKET clientSock = WSASocket(AF_INET, SOCK_STREAM, IPPROTO_TCP, nullptr, 0, WSA_FLAG_OVERLAPPED);

    char buffer[2 * (sizeof(sockaddr_in) + 16)];
    DWORD addrLen = sizeof(sockaddr_in) + 16;
    OVERLAPPED ov = {};

    lpfnAcceptEx(
        listenSocket,
        clientSock,
        buffer,
        0,
        addrLen,
        addrLen,
        nullptr,
        &ov
    );
}


void Server::AddToMatchQueue() {

}

void Server::CreateRoom(ClientSession* p1, ClientSession* p2)
{
    int roomId = _roomIdCounter++;
    rooms[roomId] = std::make_unique<Room>(
        roomId,
        p1,
        p2
	);

	std::cout << "Room " << roomId << " created for Player " << p1->GetUserId() << " and Player " << p2->GetUserId() << "\n";

    p1->Send(new
        {
            type = "MATCH_FOUND",
            roomId = roomId,
            myId = p1.userId,
            enemyId = p2.userId,
            side = "LEFT",
        });

    p2->Send(new
        {
            type = "MATCH_FOUND",
            roomId = roomId,
            myId = p2.userId,
            enemyId = p1.userId,
            side = "RIGHT",
        });
}

void Server::CloseRoom(int id) {

}

void Server::RemoveClient() {

}

void Server::TickLoop()
{
    using clock = std::chrono::steady_clock;

    const int TICK_RATE = 120;
    const int TICK_DELAY = 1000 / TICK_RATE;
    const float dt = 1.0f / TICK_RATE;

    while (true)
    {
        auto start = clock::now();

        for (auto& [id, room] : rooms)
        {
            if (!room) continue;
            room->Update(dt);
        }

        CheckHeartbeat();

        auto end = clock::now();
        auto elapsed =
            std::chrono::duration_cast<std::chrono::milliseconds>(end - start).count();

        int sleepTime = TICK_DELAY - static_cast<int>(elapsed);
        if (sleepTime > 0)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(sleepTime));
        }
    }
}

void HeartbeatLoop() {
    auto now = std::chrono::steady_clock::now();

    for (auto& [id, session] : clients) {

        if (session->disconnected)
			continue;

        auto duration = std::chrono::duration_cast<std::chrono::seconds>(now - session->lastRecvTime).count();
        if (duration > 5) {
			std::cout << "Session " << id << " time out\n";
            session->Disconnect();
        }
	}
}

void CheckHeartbeat() {

}
