#include <iostream>
#include <cstring>
#include "Server.h"
#include "ClientSession.h"
#include "Room.h"
#include "RoomEvent.h"
#include "PacketHeader.h"
#include "PacketRouter.h"
#include "LoginPacket.h"

ClientSession::ClientSession(SOCKET s, int id) : 
    socket(s), 
    sessionId(id), 
    recvBytes(0),
    disconnected(false),
    lastRecvTime(std::chrono::steady_clock::now())
{
}

void ClientSession::PostRecv()
{
    if (IsDisconnected())
        return;

    if (recvBytes >= RECV_BUFFER_SIZE)
    {
        Disconnect("recv buffer overflow / no space");
        return;
    }

    auto* ctx = new RecvContext();
    ctx->ovEx.type = IOType::Recv;
    ZeroMemory(&ctx->ovEx.ov, sizeof(OVERLAPPED));

    // 받은 바이트 뒤부터 쓰기 (데이터 혼용 방지)
    ctx->wsaBuf.buf = recvBuffer + recvBytes;

    // 버퍼 오버플로우 방지
    ctx->wsaBuf.len = RECV_BUFFER_SIZE - recvBytes;

    DWORD flag = 0;

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
            Disconnect("PostRecv error");
        }
    }
}

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

void ClientSession::SendPacket(S2C_PacketType type)
{
	SendPacketInternal(type, nullptr, 0);
}

void ClientSession::SendPacketInternal(
    S2C_PacketType type,
    const void* body,
    size_t bodySize)
{
    if (IsDisconnected())
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

    // WSASend는 WSABUF가 가리키는 메모리를 커널이 참조
    // IOCP에 완료 패킷 올라감
    int ret = WSASend(
        socket,
        &ctx->wsaBuf,
        1,
        nullptr,
        0,
        &ctx->ovEx.ov,
        nullptr
    );

    // WSASend가 즉시 완료되지 않았을 경우
    if (ret == SOCKET_ERROR)
    {
        // WSA_IO_PENDING = 비동기 처리 중 (정상)
        // 그 외 에러 = 요청 자체 실패 (delete로 memory leak 방지)
        if (WSAGetLastError() != WSA_IO_PENDING)
        {
            delete ctx;
            Disconnect("WSASend error");
        }
    }
}

// 확장용 (지금은 명시만)
void ClientSession::OnSend(DWORD bytes)
{

}

void ClientSession::OnPacket(const ParsedPacket& pkt)
{
    PacketRouter::Instance().Route(*this, pkt);
}

void ClientSession::Disconnect(const char* why) {

    if (disconnected.exchange(true))
        return;

    std::cout << "[ClientSession] Session " << sessionId << " disconnected. why=" << why << "\n";

    // 1. 통신 끊고 소켓 해제
    if (socket != INVALID_SOCKET)
    {
        shutdown(socket, SD_BOTH);
        closesocket(socket);
        socket = INVALID_SOCKET;
    }

    // 2. 룸 정리 이벤트 넘김
    if (room)
    {
        room->EnqueueEvent(RoomEvent{
            RoomEventType::Disconnect,
            sessionId
            });
    }
}
