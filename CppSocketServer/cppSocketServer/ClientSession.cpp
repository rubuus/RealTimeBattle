#include <iostream>
#include <cstring>
#include "Server.h"
#include "ClientSession.h"
#include "Room.h"
#include "RoomEvent.h"
#include "PacketHeader.h"
#include "PacketRouter.h"

ClientSession::ClientSession(SOCKET s, int id) : 
    socket(s), 
    sessionId(id), 
    recvBytes(0),
    disconnected(false),
    lastRecvTime(std::chrono::steady_clock::now())
{
}

// IOCP 기반 비동기 수신 요청 (Post Recv)
void ClientSession::PostRecv()
{
    if (IsDisconnected())
        return;

    if (recvBytes >= RECV_BUFFER_SIZE)
    {
        Disconnect("recv buffer overflow / no space");
        return;
    }

    AddIo(); // IO 추가

    auto* ctx = new RecvContext();
    ctx->ovEx.type = IOType::Recv;
    ZeroMemory(&ctx->ovEx.ov, sizeof(OVERLAPPED));

    // 받은 바이트 뒤부터 쓰기 (데이터 혼용 방지)
    ctx->wsaBuf.buf = recvBuffer + recvBytes;

    // 버퍼 오버플로우 방지
    ctx->wsaBuf.len = RECV_BUFFER_SIZE - recvBytes;

    DWORD flag = 0;

    // 소켓 스냅샷 후 체크
    SOCKET s = socket;
    if (s == INVALID_SOCKET || IsDisconnected())
    {
        // 큐에 남은 것 정리
        Disconnect("send on closed socket");
        return;
    }

    // WSARecv는 WSABUF가 가리키는 메모리를 커널이 참조
    // IOCP에 완료 패킷 올라감
    int ret = WSARecv(
        socket,
        &ctx->wsaBuf,
        1,
        nullptr,
        &flag,
        &ctx->ovEx.ov,
        nullptr
    );

    // WSARecv가 즉시 완료되지 않았을 경우
    if (ret == SOCKET_ERROR) 
    {
        // WSA_IO_PENDING = 비동기 처리 중 (정상)
        // 그 외 에러 = 요청 자체 실패 (delete로 memory leak 방지)
        if (WSAGetLastError() != WSA_IO_PENDING)
        {
            delete ctx;
            ReleaseIo(); // IO 취소
            Disconnect("PostRecv error");
        }
    }
}

// TCP 스트림 기반 패킷 파싱 처리
void ClientSession::OnRecv(DWORD bytes)
{
    SetLastRecvTime();

    recvBytes += bytes;
    size_t offset = 0;

    while (true)
    {
        // 헤더가 전체 안 왔으면 PostRecv
        if (recvBytes - offset < (int)sizeof(PacketHeader))
            break;

        PacketHeader header;
        memcpy(&header, recvBuffer + offset, sizeof(PacketHeader));


        // size 검증 (깨진 스트림 및 악성 코드)
        if (header.size < sizeof(PacketHeader) || header.size > RECV_BUFFER_SIZE) {
            Disconnect("body size out");
            return;
        }

        // 바디가 전체 안 왔으면 PostRecv
        if (recvBytes - offset < header.size)
            break;

        ParsedPacket pkt {
            header.type,
            recvBuffer + offset + sizeof(PacketHeader),
            static_cast<uint16_t>(header.size - sizeof(PacketHeader))
        };

        OnPacket(pkt);

        offset += header.size;
    }

    // 처리한 패킷 사이즈는 제거 및 메모리 위치 이동
    if (offset > 0)
    {
        memmove(recvBuffer, recvBuffer + offset, recvBytes - offset);
        recvBytes -= offset;
    }

    if (!IsDisconnected())
        PostRecv();
}

void ClientSession::SendPacket(S2C_HeaderType type)
{
	SendPacketInternal(type, nullptr, 0);
}

// 송신 데이터 큐잉
void ClientSession::SendPacketInternal(
    S2C_HeaderType type,
    const void* body,
    size_t bodySize)
{
    // 스냅샷 적용
    SOCKET s = socket;
    if (s == INVALID_SOCKET || IsDisconnected())
        return;

    // 헤더 + 바디 = 패킷 총 사이즈
    const size_t packetSize = sizeof(PacketHeader) + bodySize;

    // uint16_t 사이즈를 벗어나면 취소
    if (packetSize > UINT16_MAX)
        return;

    // 함수가 끝나도 살아있어야 되기 때문에 Heap에 생성
    // 완료 시점은 IOCP 워커 스레드
    auto* ctx = new SendContext();
    ctx->ovEx.type = IOType::Send;
    ZeroMemory(&ctx->ovEx.ov, sizeof(OVERLAPPED));

    // 패킷 사이즈만큼 버퍼 resize
    ctx->data.resize(packetSize);

    // Header 작성 (패킷 타입 + 전체 패킷 크기)
    PacketHeader header;
    header.type = static_cast<uint16_t>(type);
    header.size = static_cast<uint16_t>(packetSize);

    // 헤더와 바디는 연속된 메모리가 아니라서 memcpy 2번함
    memcpy(ctx->data.data(), &header, sizeof(header));
    memcpy(ctx->data.data() + sizeof(header), body, bodySize);

    // WSABUF에 데이터 저장
    ctx->wsaBuf.buf = ctx->data.data();
    ctx->wsaBuf.len = (ULONG)ctx->data.size();

    bool needSend = false;

    // 보낼 데이터 큐잉만 해주기
    {
        std::lock_guard<std::mutex> lock(sendMutex);
        sendQueue.push(ctx);

        if (!sending)
        {
            sending = true;
            needSend = true;
        }
    }

    if (needSend)
        PostNextSend();
}

// 커널에 비동기 송신 요청
void ClientSession::PostNextSend() 
{
    // 컨텍스트 힙 변수 설정
    SendContext* ctx = nullptr;

    {
        std::lock_guard<std::mutex> lock(sendMutex);
        if (sendQueue.empty())
        {
            sending = false;
            return;
        }
        ctx = sendQueue.front();
    }

    // 소켓 스냅샷 후 체크
    SOCKET s = socket;
    if (s == INVALID_SOCKET || IsDisconnected())
    {
        // 큐에 남은 것 정리
        Disconnect("send on closed socket");
        return;
    }

    int ret = WSASend(
        s,
        &ctx->wsaBuf,
        1,
        nullptr,
        0,
        &ctx->ovEx.ov,
        nullptr
    );

    // 에러 났을 경우
    if (ret == SOCKET_ERROR)
    {
        int werr = WSAGetLastError();
        if (werr != WSA_IO_PENDING)
        {
            // 이 요청은 커널에 안 들어갔으니 여기서 직접 제거해야 함
            {
                std::lock_guard<std::mutex> lock(sendMutex);
                if (!sendQueue.empty() && sendQueue.front() == ctx)
                {
                    sendQueue.pop();
                }
                
                sending = false;
            }

            delete ctx;
            Disconnect("WSASend error");
        }
    }
}

// 커널 처리 완료 시에 큐에서 pop + 다음 패킷 송신
void ClientSession::OnSend(DWORD bytes)
{
    bool needNext = false;
    {
        std::lock_guard<std::mutex> lock(sendMutex);
        if (!sendQueue.empty())
            sendQueue.pop();

        needNext = !sendQueue.empty();
        if (!needNext) sending = false;
    }

    if (needNext)
        PostNextSend();
}

// 패킷 데이터 라우팅
void ClientSession::OnPacket(const ParsedPacket& pkt)
{
    PacketRouter::Instance().Route(*this, pkt);
}

void ClientSession::Disconnect(const char* why) {

    if (disconnected.exchange(true))
        return;

    std::cout << "[ClientSession] Session " << sessionId << " disconnected. why=" << why << "\n";

    // 1. 통신 끊고 소켓 해제 (스냅샷으로 race condition 방지)
    SOCKET s = socket;
    socket = INVALID_SOCKET;
    shutdown(s, SD_BOTH);
    closesocket(s);

    // 2. 룸 정리 이벤트 넘김
    if (room)
    {
        room->EnqueueEvent(RoomEvent{
            RoomEventType::Disconnect,
            sessionId
            });
    }

    {
        std::lock_guard<std::mutex> lock(sendMutex);
        while (!sendQueue.empty())
        {
            delete sendQueue.front();
            sendQueue.pop();
        }
        sending = false;
    }
}
