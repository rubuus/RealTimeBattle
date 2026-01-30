#define _WINSOCK_DEPRECATED_NO_WARNINGS
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "ws2_32.lib")

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <cstring>
#include <deque>
#include <iostream>
#include <mutex>
#include <random>
#include <thread>
#include <vector>
#include <string>
#include "LifecycleBot.h"
#include "Header.h"

using namespace std::chrono_literals;

int main(int argc, char** argv) {
    const char* ip = "127.0.0.1";
    int port = 7777;
    int bots = 1000;

    if (argc >= 2) ip = argv[1];
    if (argc >= 3) port = atoi(argv[2]);
    if (argc >= 4) bots = atoi(argv[3]);

    // WinSock 초기화 (WinSock 2.2 사용)
    WSADATA w;
    WSAStartup(MAKEWORD(2, 2), &w);

    // 다수의 봇을 동시에 실행하기 위한 스레드 컨테이너
    std::vector<std::thread> ts;

    for (int i = 0; i < bots; i++) {
        ts.emplace_back([&, i]() {
            // 각 봇은 고유한 ID를 가지고 Life Cycle 테스트 수행
            LifecycleBot bot(ip, port, 1000 + i);
            bot.Run();
            std::cout << i << '\n';
        });

        // 서버 접속 스파이크 방지를 위한 간단한 딜레이
        std::this_thread::sleep_for(10ms);
    }

    // 모든 봇 쓰레드 종료 대기
    for (auto& t : ts) t.join();

    std::cout << "Finish" << '\n';
    WSACleanup();
}
