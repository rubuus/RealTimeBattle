#include <thread>
#include <iostream>
#include <chrono>
#include <winsock2.h>
#include <windows.h> 
#include <threads.h>
#include "Server.h"
#include "ClientSession.h"
#include "Room.h"
#include "MatchFoundPacket.h"
#include "PacketHeader.h"

Server& Server::Instance()
{
    static Server instance;
    return instance;
}

// Server 시작: WinSock 초기화, Socket bind 및 listening 상태 전환
// IOCP 및 WorkerThread 생성
void Server::StartServer(int port)
{
    _port = port;

    WSADATA wsaData;

    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0)
        throw std::runtime_error("WSAStartup failed");

    listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_port = htons(_port);
    addr.sin_addr.s_addr = INADDR_ANY;

    if (listenSocket == INVALID_SOCKET)
        throw std::runtime_error("socket failed");

    if (bind(listenSocket, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR)
        throw std::runtime_error("bind failed");

    if (listen(listenSocket, SOMAXCONN) == SOCKET_ERROR)
        throw std::runtime_error("listen failed");

	// IOCP 생성
    iocpHandle = CreateIoCompletionPort(INVALID_HANDLE_VALUE, nullptr, 0, 0);

    if (!iocpHandle)
        throw std::runtime_error("CreateIoCompletionPort failed");

	// hardware_concurrency()로 워커 스레드 수 결정
    unsigned int cores = std::thread::hardware_concurrency();
    if (cores == 0) cores = 4;

	// RunningThread가 멈출 경우, ReadyThread가 작업을 계속 처리할 수 있도록 2배수로 설정
    unsigned int workerCount = cores * 2;

    for (int i = 0; i < workerCount; ++i) {
        workers.emplace_back(&Server::WorkerLoop, this);
    }

    std::cout << "Server Started\n";
}

// 클라이언트 연결을 수락하고 세션을 생성하여 IOCP에 등록하는 전용 루프
void Server::AcceptLoop() 
{
    while (running)
    {
        if (listenSocket == INVALID_SOCKET) break;

        SOCKET clientSock = accept(listenSocket, nullptr, nullptr);

        if (clientSock == INVALID_SOCKET)
        {
            int err = WSAGetLastError();
            if (!running) break;

            std::cerr << "accept failed: " << err << "\n";
            continue;
        }

		// Nagle 알고리즘 비활성화
        int opt = 1;
        setsockopt(clientSock, IPPROTO_TCP, TCP_NODELAY, (const char*)&opt, sizeof(opt));

        int clientId = nextClientId.fetch_add(1);

        // ClientSession 생성
        std::unique_ptr<ClientSession> session;
        try {
            session = std::make_unique<ClientSession>(clientSock, clientId);
        }
        catch (...) {
            closesocket(clientSock);
            continue;
        }

        // IOCP에 소켓 연결
        HANDLE r = CreateIoCompletionPort(
            (HANDLE)clientSock, 
            iocpHandle,
            (ULONG_PTR)session.get(), 
            0
        );

        if (!r) {
            std::cerr << "CreateIoCompletionPort(client) failed: " << GetLastError() << "\n";
            closesocket(clientSock);
            continue;
        }

		// clients 컨테이너에 세션 등록 (mutex로 동시 접근 보호)
        ClientSession* sessionPtr = nullptr;
        {
            std::lock_guard<std::mutex> lock(clientsMutex);
            clients[clientId] = std::move(session);
            sessionPtr = clients[clientId].get();
        }

        std::cout << "[Server] Client " << clientId << " Connected\n";

		// lock 해제 후 첫 수신 요청
        if (sessionPtr) sessionPtr->PostRecv();
    }
}

void Server::WorkerLoop()
{
    while (true)
    {
        DWORD bytes = 0;
        ULONG_PTR key = 0;
        OVERLAPPED* ov = nullptr;

		// 커널이 완료 이벤트를 IOCP 큐에 넣어놓으면 꺼내 처리 (없으면 대기)
        BOOL ok = GetQueuedCompletionStatus(
            iocpHandle,
            &bytes,
            &key,
            &ov,
            INFINITE
        );

        // 종료 신호 (PQCS로 전달된 종료 이벤트)
        if (ov == nullptr && key == 0) break;

        ClientSession* session = reinterpret_cast<ClientSession*>(key);

        // null 체크
        if (!session)
            continue;
        
        // 오류 발생 또는 상대방이 연결을 정상 종료(bytes == 0)
        if (!ok || bytes == 0)
        {
            session->Disconnect();
            continue;
        }

		// 비동기 I/O에서 Recv 또는 Send 구분 처리
        auto* ovEx = reinterpret_cast<OverlappedEx*>(ov);

        if (ovEx->type == IOType::Recv) 
        {
            auto* ctx = reinterpret_cast<RecvContext*>(ovEx);
            session->OnRecv(bytes);
            delete ctx;
        }
        else
        {
            auto* ctx = reinterpret_cast<SendContext*>(ovEx);
            session->OnSend(bytes);
            delete ctx;
        }
    }
}

// 실제 게임 로직 틱 루프
void Server::TickLoop()
{
    using clock = std::chrono::steady_clock;

    constexpr int TICK_RATE = 60; // 60fps
    const auto tickDur = std::chrono::microseconds(1000000 / TICK_RATE);
	const double dt = 1.0f / TICK_RATE; // double로 정밀도 향상

    auto nextTick = clock::now();

    while (running)
    {
        if (clock::now() < nextTick)
            std::this_thread::sleep_until(nextTick);

        std::vector<Room*> snapshot;

		// snapshot 생성 (mutex로 동시 접근 보호, 락 최소화)
        {
            std::lock_guard<std::mutex> lock(roomsMutex);

			// capacity 미리 할당 (동적 재할당 방지)
            snapshot.reserve(rooms.size());

            for (auto& pair : rooms)
                snapshot.push_back(pair.second.get());
        }

		// room 업데이트
        for (auto* room : snapshot)
        {
            if (room) continue;

            room->Update(dt);
        }

        nextTick += tickDur;

        // 틱 밀림 방지
        if (clock::now() > nextTick + tickDur)
            nextTick = clock::now();
    }
}

// 1초마다 Ping 검사 및 타임아웃 처리 루프
void Server::HeartbeatLoop()
{
    while (running)
    {
        auto now = std::chrono::steady_clock::now();

        // 임시 상태 판단 vector (루프마다 초기화)
        std::vector<ClientSession*> timeoutList;
        std::vector<ClientSession*> aliveList;

		// 모든 클라이언트 세션을 검사하며 vector에 분류 (mutex로 동시 접근 보호)
        {
            std::lock_guard<std::mutex> lock(clientsMutex);

            for (auto& pair : clients)
            {
                auto duration =
                    std::chrono::duration_cast<std::chrono::seconds>(
                        now - pair.second->GetLastRecvTime()).count();

                // 상태 분류
                if (duration > 5)
                    timeoutList.push_back(pair.second.get());
                else if (!pair.second->IsDisconnected())
                    aliveList.push_back(pair.second.get());
            }
        }

		// 타임아웃 세션 처리
        for (auto s : timeoutList)
        {
            std::cout << "Session timed out\n";
            s->Disconnect();
        }

        // 살아있는 세션에 heartbeat 응답(PONG) 전송
        for (auto s : aliveList)
        {
            s->SendPacket(S2C_PacketType::PONG);
        }

        std::this_thread::sleep_for(std::chrono::seconds(1));
    }
}

// Server 종료: 모든 스레드 종료 대기 및 WinSock 정리
void Server::StopServer()
{
    running = false;

	// 모든 WorkerThread를 깨움
    for (size_t i = 0; i < workers.size(); ++i)
        PostQueuedCompletionStatus(iocpHandle, 0, 0, nullptr);

	// WorkerThread 종료 대기
    for (auto& w : workers)
        if (w.joinable())
            w.join();

	// listenSocket을 닫아 Accept Loop 중단
    closesocket(listenSocket);

	// WinSock 정리
    WSACleanup();
}

// 세션을 대기열에 추가하고 매칭 시도
void Server::AddToMatchList(int sid)
{
    int a = -1, b = -1;

	// matchList 접근은 mutex로 보호
    {
		std::lock_guard<std::mutex> lock(matchMutex);
        matchList.push_back(sid);

        if (matchList.size() < 2) return;

        a = matchList.front(); matchList.pop_front();
        b = matchList.front(); matchList.pop_front();
    }

    ClientSession* p1 = nullptr;
    ClientSession* p2 = nullptr;

    bool aValid = false;
    bool bValid = false;

    // 세션 존재 여부 확인
    {
        std::lock_guard<std::mutex> lock(clientsMutex);

        auto it1 = clients.find(a);
        auto it2 = clients.find(b);

        if (it1 != clients.end()) {
            p1 = it1->second.get();
            aValid = true;
        }
        if (it2 != clients.end()) {
            p2 = it2->second.get();
            bValid = true;
        }
    }

    // 실패 시 되돌림 (살아있는 쪽만)
    if (!aValid || !bValid)
    {
        std::lock_guard<std::mutex> lock(matchMutex);

        if (aValid) matchList.push_front(a);
        if (bValid) matchList.push_front(b);

        return;
    }

    CreateRoom(p1, p2);
}

// 두 세션으로 새 룸 생성
void Server::CreateRoom(ClientSession* p1, ClientSession* p2)
{
    int roomId = _roomIdCounter++;

    Room* roomPtr = nullptr;

	// rooms 컨테이너에 룸 생성 및 등록 (mutex로 동시 접근 보호)
    {
        std::lock_guard<std::mutex> lock(roomsMutex);
        rooms[roomId] = std::make_unique<Room>(roomId, p1, p2, threadPool);
        roomPtr = rooms[roomId].get();
    }

    p1->SetRoom(roomPtr);
    p2->SetRoom(roomPtr);

    std::cout << "Room " << roomId << " created for Player " << p1->GetUserId() << " and Player " << p2->GetUserId() << "\n";

	NotifyMatchFound(roomId, p1, p2);
}

void Server::NotifyMatchFound(int roomId, ClientSession* p1, ClientSession* p2) {
    p1->SendPacket(S2C_PacketType::MATCH_FOUND, MatchFoundPacket(roomId, p1->GetUserId(), p2->GetUserId(), Side::Left));
    p2->SendPacket(S2C_PacketType::MATCH_FOUND, MatchFoundPacket(roomId, p2->GetUserId(), p1->GetUserId(), Side::Right));
}

void Server::CloseRoom(int id) {
    std::unique_ptr<Room> dying;

	// rooms 컨테이너에서 룸 제거 (락 범위 최소화)
    {
        std::lock_guard<std::mutex> lock(roomsMutex);

        auto it = rooms.find(id);

        if (it == rooms.end()) return;

        dying = std::move(it->second);
        rooms.erase(it);
    }

    // 락 밖에서 정리
    if (dying)
    {
        dying->CloseRoom();
    }
}

void Server::RemoveClient(ClientSession* s)
{
    if (!s) return;
    const int sid = s->GetSessionId();

    // matchList에서 제거 (락 범위 최소화)
    {
        std::lock_guard<std::mutex> lock(matchMutex);
        matchList.remove(sid);
    }

    std::unique_ptr<ClientSession> dying;

    // clients map에서 소유권 이동
    {
        std::lock_guard<std::mutex> lock(clientsMutex);
        auto it = clients.find(sid);

        if (it != clients.end())
        {
            dying = std::move(it->second);
            clients.erase(it);
        }
    }

    // 락 밖에서 정리
    if (dying)
    {
        dying->Disconnect();
    }

    std::cout << "[Server] Client " << sid << " Removed and Resource Cleaned\n";
}