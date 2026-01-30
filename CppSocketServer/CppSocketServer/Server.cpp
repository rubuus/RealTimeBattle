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

        // 수신시간 갱신
        sessionPtr->SetLastRecvTime();
		// lock 해제 후 첫 수신 요청
        if (sessionPtr) sessionPtr->PostRecv();
    }
}

// IOCP 큐에 이벤트가 있을 시, 세션에 이벤트 넘겨주는 Thread
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
        if (!session || !ov)
            continue;

        // Heap에 저장 (ovEx -> ctx)
        // WSASend 함수가 끝나도 IO는 나중에 처리 (커널 댕글링 참조 방지)
        auto* ovEx = reinterpret_cast<OverlappedEx*>(ov);
        void* ctx = nullptr;

        // 오류 발생
        if (!ok)
        {
            DWORD err = GetLastError();
            std::cout << "[GQCS !ok] err=" << err << "\n";
            session->Disconnect("GQCS !ok");
            ctx = ovEx;
        }
        else if (session->IsDisconnected())
        {
            ctx = ovEx;
        }
        // Recv 정상 종료 (bytes==0)
        else if (ovEx->type == IOType::Recv && bytes == 0)
        {
            session->Disconnect("Recv bytes==0");
            ctx = ovEx;
        }
        // 정상 처리
        else
        {
            if (ovEx->type == IOType::Recv)
                session->OnRecv(bytes);
            else
                session->OnSend(bytes);

            ctx = ovEx;
        }

        // Heap 변수 컨텍스트 해제
        // 완료, 실패 어떤 상황이든 delete로 memory leak 방지
        if (ctx)
        {
            if (ovEx->type == IOType::Recv) 
                delete reinterpret_cast<RecvContext*>(ctx);
            else                            
                delete reinterpret_cast<SendContext*>(ctx);
        }

        // IO 완료 카운트 감소는 항상 여기서 1번
        session->ReleaseIo();

        // 바로 세션 정리가 아닌 큐에 저장 후, 다른 쓰레드에서 처리
        if (session->CanCleanup() && !session->cleanupQueued.exchange(true))
        {
            cleanupQueue.push(session->GetSessionId());
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

		// room 업데이트 (최대 2틱까지만 틱 밀림 보장)
        int maxCatchup = 2;
        while (clock::now() >= nextTick && maxCatchup-- > 0)
        {
            for (auto* room : snapshot) 
            {
                room->Update(dt);

                if (room->IsClosed())
                    closedRoom.push(room->GetRoomId());
            }
                
            nextTick += tickDur;
        }

        // 너무 밀렸다면 리셋
        if (clock::now() > nextTick + tickDur)
            nextTick = clock::now();

        ProcessClosedRooms();
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

        // 스냅샷 생성
        std::vector<ClientSession*> snapshot;
        {
            std::lock_guard<std::mutex> lock(clientsMutex);

            // capacity 미리 할당 (동적 재할당 방지)
            snapshot.reserve(clients.size());
            
            for (auto& pair : clients)
               snapshot.push_back(pair.second.get());

        }

        for (auto& s : snapshot) 
        {
            // 모든 클라이언트 세션을 검사하며 vector에 분류
            if (s->IsDisconnected())
                continue;

            // 세션 응답 주기 갱신
            auto duration =
                std::chrono::duration_cast<std::chrono::seconds>(
                    now - s->GetLastRecvTime()).count();

            // 5초 이상 응답 없을 시, time out
            if (duration > 5)
                timeoutList.push_back(s);
            else
                aliveList.push_back(s);
        }
        
		// 타임아웃 세션 처리
        for (auto s : timeoutList)
        {
            std::cout << "Session timed out\n";
            s->Disconnect("Heartbeat timeout");

            if (s->CanCleanup() && !s->cleanupQueued.exchange(true))
            {
                cleanupQueue.push(s->GetSessionId());
            }
        }

        // 살아있는 세션에 heartbeat 응답(PONG) 전송
        for (auto s : aliveList)
        {
            s->SendPacket(S2C_HeaderType::PONG);
        }

        std::this_thread::sleep_for(std::chrono::seconds(1));
    }
}

// 세션 정리 전용 루프
void Server::CleanupLoop()
{
    while (running)
    {
        int sid = -1;

        if (cleanupQueue.try_pop(sid))
        {
            if (!RemoveClient(sid))
            {
                cleanupQueue.push(sid);
            }
        }
        else
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    }
}

// Server 종료: 모든 스레드 종료 대기 및 WinSock 정리
// 특정 상황 아니면 서버는 계속 돌기 때문에 확장용
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

        if (std::find(matchList.begin(), matchList.end(), sid) != matchList.end())
            return;

        matchList.push_back(sid);

        if (matchList.size() < 2) return;

        a = matchList.front(); matchList.pop_front();
        b = matchList.front(); matchList.pop_front();
    }

    ClientSession* p1 = nullptr;
    ClientSession* p2 = nullptr;

    // 세션 존재 여부 확인
    {
        std::lock_guard<std::mutex> lock(clientsMutex);

        auto it1 = clients.find(a);
        auto it2 = clients.find(b);

        if (it1 != clients.end()) {
            p1 = it1->second.get();
        }
        if (it2 != clients.end()) {
            p2 = it2->second.get();
        }
    }

    // 실패 시 되돌림 (살아있는 쪽만)
    if (a == -1 || b == -1)
    {
        std::lock_guard<std::mutex> lock(matchMutex);

        if (a != -1) matchList.push_front(a);
        if (b != -1) matchList.push_front(b);

        return;
    }

    CreateRoom(p1, p2);
}

// 매칭 취소 시, 매칭 대기열에서 세션 제거
void Server::CancelMatch(int sid)
{
    std::lock_guard<std::mutex> lock(matchMutex);

    auto it = std::find(matchList.begin(), matchList.end(), sid);
    if (it != matchList.end())
        matchList.erase(it);
}

// 두 세션으로 새 룸 생성
void Server::CreateRoom(ClientSession* p1, ClientSession* p2)
{
    // 한 클라에서 2번 매칭 시도로 룸 생성 방지
    if (p1 == p2 || p1->GetSessionId() == p2->GetSessionId())
        return;

    if (p1->IsDisconnected() || p2->IsDisconnected())
        return;

    int roomId = _roomIdCounter++;

    int p1uid = p1->GetUserId();
    int p1sid = p1->GetSessionId();

    int p2uid = p2->GetUserId();
    int p2sid = p2->GetSessionId();

    Room* roomPtr = nullptr;

	// rooms 컨테이너에 룸 생성 및 등록 (mutex로 동시 접근 보호)
    {
        std::lock_guard<std::mutex> lock(roomsMutex);
        rooms[roomId] = std::make_unique<Room>(roomId, p1uid, p1sid, p2uid, p2sid, threadPool);
        roomPtr = rooms[roomId].get();
    }

    p1->SetRoomId(roomId);
    p2->SetRoomId(roomId);

    std::cout << "Room " << roomId << " created for Player " << p1->GetUserId() << " and Player " << p2->GetUserId() << "\n";

	NotifyMatchFound(roomId, p1, p2);
}

void Server::NotifyMatchFound(int roomId, ClientSession* p1, ClientSession* p2) {
    p1->SendPacket(S2C_HeaderType::MATCH_FOUND, MatchFoundPacket(roomId, p1->GetUserId(), p1->GetSessionId(), p2->GetUserId(), p2->GetSessionId(), Side::Left));
    p2->SendPacket(S2C_HeaderType::MATCH_FOUND, MatchFoundPacket(roomId, p2->GetUserId(), p2->GetSessionId(), p1->GetUserId(), p1->GetSessionId(), Side::Right));
}

// 서버에서 컨트롤
void Server::EnqueueRoomEvent(int roomId, const RoomEvent& ev)
{
    std::lock_guard<std::mutex> lock(roomsMutex);

    auto it = rooms.find(roomId);
    if (it == rooms.end())
        return;

    it->second->EnqueueEvent(ev);
}

// 닫힌 Room Id만 복사해서 한번에 처리
void Server::ProcessClosedRooms()
{
    std::queue<int> local;
    {
        std::lock_guard<std::mutex> lock(closedRoomMutex);
        std::swap(local, closedRoom);
    }

    while (!local.empty())
    {
        CloseRoom(local.front());
        local.pop();
    }
}

void Server::CloseRoom(int id) {
    Room* room = nullptr;

    {
        std::lock_guard<std::mutex> lock(roomsMutex);

        auto it = rooms.find(id);
        if (it == rooms.end())
            return;

        room = it->second.get(); // 아직 살아 있음
    }

    // 반드시 erase 전에 정리
    room->CloseRoom();

    {
        std::lock_guard<std::mutex> lock(roomsMutex);

        auto it = rooms.find(id);
        if (it != rooms.end())
            rooms.erase(it); // 이제 delete
    }
}

bool Server::RemoveClient(int sid)
{
    // 매칭리스트에서 세션 제거
    {
        std::lock_guard<std::mutex> lock(matchMutex);
        matchList.remove(sid);
    }

    std::unique_ptr<ClientSession> dying;

    // 세션 정리할 준비가 되면 소유권 넘긴 후, clients 컨테이너에서 제거
    {
        std::lock_guard<std::mutex> lock(clientsMutex);
        auto it = clients.find(sid);

        if (it == clients.end())
            return true;

        ClientSession* s = it->second.get();

        int userId = s->GetUserId();
        if (userId != 0)
        {
            PacketRouter::Instance().onlineUsers.erase(userId);
        }

        // cleanup 가능 여부 판단
        if (!s->CanCleanup())
            return false;

        dying = std::move(it->second);

        std::cout << "[SESSION REMOVE] sid=" << sid << "\n";

        // 빈 unique_ptr 소멸
        clients.erase(it);
    }

    std::cout << "[Server] Client " << sid << " Removed and Resource Cleaned\n";
    return true;
}
