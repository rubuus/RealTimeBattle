#include "Server.h"
#include <thread>
#include <iostream>
#include <timeapi.h>
#pragma comment(lib, "winmm.lib")

// 자동 소멸 : 윈도우 타이머 해상도 설정
struct TimerResolutionGuard {
    explicit TimerResolutionGuard(UINT ms) : ms(ms) { timeBeginPeriod(ms); }
    ~TimerResolutionGuard() { timeEndPeriod(ms); }
    UINT ms;
};

int main()
{
    TimerResolutionGuard timer(1);

    constexpr int PORT = 7777;

    try
    {
        Server& server = Server::Instance();

        server.StartServer(PORT);

        std::thread acceptThread(&Server::AcceptLoop, &server);
        std::thread tickThread(&Server::TickLoop, &server);
        std::thread heartbeatThread(&Server::HeartbeatLoop, &server);
        std::thread cleanupThread(&Server::CleanupLoop, &server);

        // Thread 대기
        acceptThread.join();
        tickThread.join();
        heartbeatThread.join();
        cleanupThread.join();
    }
    catch (const std::exception& e)
    {
        std::cerr << "Fatal error: " << e.what() << '\n';
    }

    return 0;
}
