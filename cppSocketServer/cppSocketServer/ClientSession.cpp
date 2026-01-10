#include "ClientSession.h"
#include <iostream>
#include <cstring>
#include "Server.h"
#include "Room.h"
#include "LoginPacket.h"
#include "PacketRouter.h"

ClientSession::ClientSession(SOCKET s, int id)
    : socket(s), sessionId(id)
{
    recvOvEx.type = IOType::Recv;
    ZeroMemory(&recvOvEx.ov, sizeof(OVERLAPPED));
}

void ClientSession::PostRecv()
{
    if (IsDisconnected())
        return;

    DWORD flags = 0;
    ZeroMemory(&recvOvEx.ov, sizeof(OVERLAPPED));

    recvWsaBuf.buf = recvBuffer + recvBytes;
    recvWsaBuf.len = RECV_BUFFER_SIZE - recvBytes;

    int ret = WSARecv(
        socket,
        &recvWsaBuf,
        1,
        nullptr,
        &flags,
        &recvOvEx.ov,
        nullptr
    );

    if (ret == SOCKET_ERROR)
    {
        int err = WSAGetLastError();
        if (err != WSA_IO_PENDING)
            Disconnect();
    }
}

void ClientSession::OnRecv(int bytes)
{
    if (bytes == 0) {
        Disconnect();
        return;
    }

    SetLastRecvTime();

    recvBytes += bytes;
    int offset = 0;

    while (true)
    {
        if (recvBytes - offset < (int)sizeof(PacketHeader))
            break;

        auto* header = reinterpret_cast<PacketHeader*>(recvBuffer + offset);

        // size 검증 (최소/최대)
        if (header->size < sizeof(PacketHeader) || header->size > RECV_BUFFER_SIZE) {
            Disconnect();
            return;
        }

        if (recvBytes - offset < header->size)
            break;

        HandlePacket(recvBuffer + offset, header->size);
        offset += header->size;
    }

    if (offset > 0)
    {
        memmove(recvBuffer, recvBuffer + offset, recvBytes - offset);
        recvBytes -= offset;
    }

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

    const size_t packetSize = sizeof(PacketHeader) + bodySize;

    auto* ctx = new SendContext();
    ctx->ovEx.type = IOType::Send;
    ZeroMemory(&ctx->ovEx.ov, sizeof(OVERLAPPED));

    ctx->data.resize(packetSize);

    // 1) Header
    PacketHeader header;
    header.type = static_cast<uint16_t>(type);
    header.size = static_cast<uint16_t>(packetSize);

    memcpy(ctx->data.data(), &header, sizeof(header));

    // 2) Body
    if (body && bodySize > 0)
    {
        memcpy(
            ctx->data.data() + sizeof(PacketHeader),
            body,
            bodySize
        );
    }

    ctx->wsaBuf.buf = ctx->data.data();
    ctx->wsaBuf.len = (ULONG)ctx->data.size();

    int ret = WSASend(
        socket,
        &ctx->wsaBuf,
        1,
        nullptr,
        0,
        &ctx->ovEx.ov,
        nullptr
    );

    if (ret == SOCKET_ERROR)
    {
        if (WSAGetLastError() != WSA_IO_PENDING)
        {
            delete ctx;
            Disconnect();
        }
    }
}

void ClientSession::OnPacket(const ParsedPacket& pkt)
{
    PacketRouter::Instance().Route(*this, pkt);
}

void ClientSession::Disconnect() {
    if (disconnected.exchange(true))
        return;

    std::cout << "[ClientSession] Session " << sessionId << " disconnected.\n";

    if (socket != INVALID_SOCKET)
    {
        shutdown(socket, SD_BOTH);
        closesocket(socket);
        socket = INVALID_SOCKET;
    }

    // 2) 룸 정리 이벤트 넘김
    if (room)
    {
        room->EnqueueEvent(RoomEvent{
            RoomEventType::Disconnect,
            sessionId
            });
    }

    // 3) 클라이언트 목록 제거
    Server::Instance().RemoveClient(this);
}