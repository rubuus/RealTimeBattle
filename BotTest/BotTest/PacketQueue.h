#pragma once
#include "Header.h"

struct RecvPacket {
    uint16_t type;
};

// 수신된 패킷 타입을 저장하고,
// 특정 패킷(type)이 도착할 때까지 대기할 수 있는 동기화 큐
class PacketQueue {
public:
    // 수신된 패킷 타입을 큐에 추가하고 대기 중인 스레드에 알림
    void Push(uint16_t t) {
        std::lock_guard<std::mutex> lk(m);
        q.push_back(t);
        cv.notify_all();
    }

    // 지정된 시간 동안 특정 패킷 타입(want)이 수신될 때까지 대기
    // 성공 시 해당 패킷을 큐에서 제거하고 true 반환
    // 타임아웃 시 false 반환
    bool Wait(uint16_t want, std::chrono::milliseconds timeout) {
        auto end = std::chrono::steady_clock::now() + timeout;
        std::unique_lock<std::mutex> lk(m);

        while (true) {
            for (auto it = q.begin(); it != q.end(); ++it) {
                if (*it == want) {
                    q.erase(it);
                    return true;
                }
            }

            if (cv.wait_until(lk, end) == std::cv_status::timeout)
                return false;
        }
    }

private:
    std::mutex m;
    std::condition_variable cv;
    std::deque<uint16_t> q;
};