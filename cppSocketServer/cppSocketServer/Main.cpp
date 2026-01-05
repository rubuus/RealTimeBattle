#include "Server.h"
#include <thread>
#include <iostream>

int main()
{
    constexpr int PORT = 7777;

    try
    {
        Server& server = Server::Instance();

        // 서버 시작 (listen + IOCP + accept)
        server.StartServer(PORT);

        // Tick / Heartbeat는 보통 별도 스레드
        std::thread tickThread(&Server::TickLoop, &server);
        std::thread heartbeatThread(&Server::HeartbeatLoop, &server);

        tickThread.join();
        heartbeatThread.join();
    }
    catch (const std::exception& e)
    {
        std::cerr << "Fatal error: " << e.what() << std::endl;
    }

    return 0;
}
