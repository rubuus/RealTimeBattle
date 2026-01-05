#include "Server.h"
#include "ClientSession.h"
#include "Room.h"
#include "MatchFoundPacket.h"
#include <thread>
#include <iostream>
#include <chrono>
#include <winsock2.h>
#include <windows.h> 

Server& Server::Instance()
{
    static Server instance;
    return instance;
}

void Server::StartServer(int port) {
    _port = port;

    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);

    listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_port = htons(_port);
    addr.sin_addr.s_addr = INADDR_ANY;

    bind(listenSocket, (sockaddr*)&addr, sizeof(addr));
    listen(listenSocket, SOMAXCONN);

    // ✅ IOCP 생성
    iocpHandle = CreateIoCompletionPort(INVALID_HANDLE_VALUE, nullptr, 0, 0);
    if (!iocpHandle) {
        std::cerr << "CreateIoCompletionPort failed: " << GetLastError() << "\n";
        return;
    }

    // ✅ 워커 스레드 생성 (일단 2~4개 정도)
    const int workerCount = 4;
    for (int i = 0; i < workerCount; ++i) {
        workers.emplace_back(&Server::WorkerLoop, this);
    }

    std::cout << "Server Started\n";

    // ✅ accept 루프는 메인 스레드에서 돌려도 되고, 별도 스레드로 빼도 됨
    AcceptLoop();
}

void Server::AcceptLoop() {
    int clientId = 1;

    while (true)
    {
        SOCKET clientSock = accept(listenSocket, nullptr, nullptr);
        if (clientSock == INVALID_SOCKET)
            continue;

        // 세션 생성
        auto session = std::make_unique<ClientSession>(clientSock, clientId);

        // ✅ IOCP에 소켓 연결 (CompletionKey = session 포인터)
        HANDLE r = CreateIoCompletionPort((HANDLE)clientSock, iocpHandle,
            (ULONG_PTR)session.get(), 0);
        if (!r) {
            std::cerr << "CreateIoCompletionPort(client) failed: " << GetLastError() << "\n";
            closesocket(clientSock);
            continue;
        }

        {
            std::lock_guard<std::mutex> lock(clientsMutex);
            clients[clientId] = std::move(session);
        }

        std::cout << "[Server] Client " << clientId << " Connected\n";

        // ✅ 반드시 첫 PostRecv로 수신 시작
        {
            std::lock_guard<std::mutex> lock(clientsMutex);
            clients[clientId]->PostRecv();
        }

        clientId++;
    }
}

void Server::WorkerLoop() {
    while (true)
    {
        DWORD bytes = 0;
        ULONG_PTR key = 0;
        OVERLAPPED* ov = nullptr;

        BOOL ok = GetQueuedCompletionStatus(
            iocpHandle,
            &bytes,
            &key,
            &ov,
            INFINITE
        );

        auto* session = reinterpret_cast<ClientSession*>(key);

        // ov == nullptr인 경우도 있을 수 있음 (포트 깨짐 등)
        if (!session || !ov || !ok) {
            if (session) session->Disconnect();
            continue;
        }

        // ✅ ov로 recv/send 구분
        auto* ovEx = reinterpret_cast<OverlappedEx*>(ov);

        if (ovEx->type == IOType::Recv)
        {
            session->OnRecv((int)bytes);
        }
        else // Send
        {
            // Send는 컨텍스트를 new로 만들어서 ov 포인터가 컨텍스트 내부를 가리키게 했다고 가정
            // OverlappedEx가 컨텍스트 첫 멤버면 캐스팅 가능
            delete reinterpret_cast<SendContext*>(ovEx);
        }
    }
}


void Server::AddToMatchQueue(int sid)
{
    matchQueue.push(sid);

    if (matchQueue.size() >= 2)
    {
        int a = matchQueue.front(); matchQueue.pop();
        int b = matchQueue.front(); matchQueue.pop();

        ClientSession* p1 = nullptr;
        ClientSession* p2 = nullptr;

        { // clients 접근은 락
            std::lock_guard<std::mutex> lock(clientsMutex);
            auto it1 = clients.find(a);
            auto it2 = clients.find(b);
            if (it1 == clients.end() || it2 == clients.end()) return;
            p1 = it1->second.get();
            p2 = it2->second.get();
        }

        CreateRoom(p1, p2);
    }
}


void Server::CreateRoom(ClientSession* p1, ClientSession* p2)
{
    int roomId = _roomIdCounter++;
    rooms[roomId] = std::make_unique<Room>(
        roomId,
        p1,
        p2,
        threadPool
	);

	std::cout << "Room " << roomId << " created for Player " << p1->GetUserId() << " and Player " << p2->GetUserId() << "\n";

    p1->SendPacket(S2C_PacketType::MATCH_FOUND, MatchFoundPacket(roomId, p1->GetUserId(), p2->GetUserId(), Side::Left));
    p2->SendPacket(S2C_PacketType::MATCH_FOUND, MatchFoundPacket(roomId, p2->GetUserId(), p1->GetUserId(), Side::Right));
}

void Server::CloseRoom(int id) {
    auto it = rooms.find(id);
    if (it == rooms.end())
        return;

    it->second->CloseRoom();
    rooms.erase(id);
}

void Server::RemoveClient(ClientSession* s)
{
    if (!s) return;

    const int sid = s->GetSessionId();

    // 1) matchQueue에서 제거
    std::queue<int> newQueue;
    while (!matchQueue.empty())
    {
        int cur = matchQueue.front();
        matchQueue.pop();

        if (cur != sid)
            newQueue.push(cur);
    }
    matchQueue = std::move(newQueue);

    // 2) clients map에서 제거
    auto it = clients.find(sid);
    if (it != clients.end())
        clients.erase(it);
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

        std::lock_guard<std::mutex> lock(roomsMutex);
        for (auto& pair : rooms)
        {
            auto& room = pair.second;
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

void Server::HeartbeatLoop()
{
    while (true)
    {
        {
            std::lock_guard<std::mutex> lock(clientsMutex);

            for (auto& pair : clients)
            {
                if (pair.second->IsDisconnected())
                    continue;

                pair.second->SendPacket(S2C_PacketType::PING);
            }
        }

        std::this_thread::sleep_for(std::chrono::seconds(1));
    }
}


void Server::CheckHeartbeat()
{
    auto now = std::chrono::steady_clock::now();

    std::lock_guard<std::mutex> lock(clientsMutex);

    for (auto& pair : clients)
    {
        if (!pair.second || pair.second->IsDisconnected())
            continue;

        auto duration = std::chrono::duration_cast<std::chrono::seconds>(
            now - pair.second->GetLastRecvTime()
        ).count();

        if (duration > 5)
        {
            std::cout << "Session " << pair.first << " time out\n";
            pair.second->Disconnect();
        }
    }
}