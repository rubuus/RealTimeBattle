#include "ClientSession.h"

ClientSession::ClientSession(SOCKET s, int id)
    : socket(s), sessionId(id)
{
}

void ClientSession::SendPacket(S2C_PacketType type)
{
    PacketHeader header;
    header.type = static_cast<uint16_t>(type);
    header.size = sizeof(PacketHeader);

    Send(reinterpret_cast<const char*>(&header), sizeof(header));
}

void ClientSession::Send(const char* data, int len)
{
    WSABUF wsaBuf;
    wsaBuf.buf = const_cast<char*>(data);
    wsaBuf.len = len;

    OVERLAPPED* ov = new OVERLAPPED();
    ZeroMemory(ov, sizeof(OVERLAPPED));

    int ret = WSASend(
        socket,
        &wsaBuf,
        1,
        NULL,        // IOCP에서는 NULL
        0,
        ov,          // 중요
        NULL
    );

    if (ret == SOCKET_ERROR)
    {
        int err = WSAGetLastError();
        if (err != WSA_IO_PENDING)
        {
            delete ov;
            Disconnect();
        }
    }
}

void ClientSession::OnPong() {
    lastRecvTime = std::chrono::steady_clock::now();
}

void ClientSession::Disconnect() {
    if (!disconnected)
        return;

}
