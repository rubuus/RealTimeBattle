#pragma once
#include "PacketQueue.h"
#include "Header.h"

using namespace std::chrono_literals;

class LifecycleBot {
    std::thread recvThread;
    std::thread pingThread;
    std::atomic<bool> running{ true };

public:
    LifecycleBot(const char* ip, int port, int id)
        : ip(ip), port(port), userId(id) {
    }

    // ping recv 쓰레드 실행
    void StartWorker() {
        recvThread = std::thread(&LifecycleBot::RecvLoop, this);
        pingThread = StartPingLoop(sock, running);
    }

    // 지정된 길이만큼 전송 완료될 때까지 반복 (TCP 특성상 패킷 크기가 보장 안되기 때문)
    static bool SendAll(SOCKET s, const char* data, int len) {
        int sent = 0;
        while (sent < len) {
            int r = send(s, data + sent, len - sent, 0);
            if (r <= 0) return false;
            sent += r;
        }
        return true;
    }

    // 안전하게 패킷 내용 복사 후, 보내기
    template<typename T>
    static bool SendPacket(SOCKET s, C2S type, const T& body) {
        PacketHeader h;
        h.type = (uint16_t)type;
        h.size = (uint16_t)(sizeof(PacketHeader) + sizeof(T));

        std::vector<char> buf(h.size);

        memcpy(buf.data(), &h, sizeof(h));
        memcpy(buf.data() + sizeof(h), &body, sizeof(T));

        return SendAll(s, buf.data(), (int)buf.size());
    }

    // 헤더만 있을 경우, 패킷 헤더 사이즈만 보내기
    static bool SendHeaderOnly(SOCKET s, C2S type) {
        PacketHeader h;
        h.type = (uint16_t)type;
        h.size = sizeof(PacketHeader);

        return SendAll(s, (char*)&h, sizeof(h));
    }

    // 1초마다 서버에 Ping 패킷 전송
    std::thread StartPingLoop(SOCKET sock, std::atomic<bool>& running)
    {
        return std::thread([sock, &running]() {
            while (running.load(std::memory_order_acquire)) {
                SendHeaderOnly(sock, C2S::PING);
                std::this_thread::sleep_for(std::chrono::seconds(1));
            }
        });
    }

    // ping recv 쓰레드 종료 대기
    void Stop() {
        running.store(false, std::memory_order_release);

        if (pingThread.joinable())
            pingThread.join();

        if (recvThread.joinable())
            recvThread.join();
    }

    // Life Cycle 실행
    bool Run() {
        if (!Connect()) return false;

        StartWorker();

        LOG("SEND LOGIN");
        SendPacket(sock, C2S::LOGIN_TEST, LoginBody{ userId });

        SendHeaderOnly(sock, C2S::MATCH_START);

        if (!Wait(S2C::MATCH_FOUND, 5s)) return Fail("MATCH_FOUND timeout");

        LOG("SEND BATTLE_READY");
        SendHeaderOnly(sock, C2S::BATTLE_READY);

        if (!Wait(S2C::LOAD_BATTLE, 5s)) return Fail("LOAD_BATTLE timeout");

        LOG("SEND BATTLE_START");
        SendHeaderOnly(sock, C2S::BATTLE_START);

        // 인풋 3초
        SendInputsFor(3s);

        // 결과 중 하나 와야 정상 종료
        if (!(
            Wait(S2C::GAME_WIN, 3s) ||
            Wait(S2C::GAME_LOSE, 3s) ||
            Wait(S2C::GAME_DRAW, 3s) ||
            Wait(S2C::ENEMY_EXIT, 3s)))
        {
            return Fail("NO GAME RESULT");
        }

        // 클라에서 끝났다는 신호 보내기
        LOG("SEND RESULT_ACK");
        SendHeaderOnly(sock, C2S::RESULT_ACK);

        // 서버에서 룸 닫혔다는 신호 받기
        if (!Wait(S2C::ROOM_CLOSED, 5s)) return Fail("ROOM_CLOSED timeout");

        LOG("LIFECYCLE OK");
        return true;
    }

    // 모든 행동 끝나면 객체 소멸 및 소켓 제거
    ~LifecycleBot()
    {
        shutdown(sock, SD_BOTH);
        Stop();
        closesocket(sock);
    }

private:
    SOCKET sock = INVALID_SOCKET;
    const char* ip;
    int port;
    int userId;
    PacketQueue packets;

    void LOG(const char* msg) {
        std::cout << "[Bot " << userId << "] " << msg << "\n";
    }

    bool Fail(const char* why) {
        std::cout << "[Bot " << userId << "] ❌ FAIL: " << why << "\n";
        return false;
    }

    // TCP 소켓 생성 및 서버 접속 시도
    bool Connect() {
        sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        sockaddr_in addr{};
        addr.sin_family = AF_INET;
        addr.sin_port = htons(port);
        inet_pton(AF_INET, ip, &addr.sin_addr);

        if (connect(sock, (sockaddr*)&addr, sizeof(addr)) != 0)
            return Fail("connect failed");

        return true;
    }

    // 서버 응답(S2C 패킷)을 일정 시간 동안 대기
    bool Wait(S2C type, std::chrono::milliseconds t) {
        std::string msg = "WAIT " + std::to_string((int)type);
        LOG(msg.c_str());
        return packets.Wait((uint16_t)type, t);
    }

    std::vector<char> recvBuf;
    size_t recvBytes = 0;

    // 클라이언트 Recv 루프
    void RecvLoop() {
        std::vector<char> rb(8192);
        size_t recvBytes = 0;

        while (true) {
            int r = recv(sock, rb.data() + recvBytes, (int)(rb.size() - recvBytes), 0);
            if (r <= 0) break; // 연결 종료 및 에러
            recvBytes += r; // 바이트 스트림 계속 누적

            size_t offset = 0;
            while (true) {

                // 헤더 전체 수신 못함
                if (recvBytes - offset < sizeof(PacketHeader)) break;

                auto* h = reinterpret_cast<PacketHeader*>(rb.data() + offset);

                // size 검증 (패킷 크기가 너무 작거나 너무 크면 컷)
                if (h->size < sizeof(PacketHeader) || h->size > rb.size()) {
                    std::cout << "[BOT RECV] BAD size=" << h->size << " type=" << h->type << "\n";
                    return;
                }

                // 바디 전체 수신 못함
                if (recvBytes - offset < h->size) break;

                // 여기서 타입 확인 가능
                std::cout << "[BOT RECV] type=" << h->type << " size=" << h->size << "\n";
                
                // Send Queue에 큐잉만
                packets.Push(h->type);

                offset += h->size;
            }

            // 처리 완료된 데이터를 제거하고, 남은 수신 데이터를 버퍼 앞으로 이동
            if (offset > 0) {
                memmove(rb.data(), rb.data() + offset, recvBytes - offset);
                recvBytes -= offset;
            }
        }
    }

    // 설정된 시간 동안, 랜덤 인풋 값 생성 후, 50ms 주기로 서버에 인풋 패킷 전송
    void SendInputsFor(std::chrono::milliseconds dur) {
        auto end = std::chrono::steady_clock::now() + dur;
        std::mt19937 rng(userId);

        while (std::chrono::steady_clock::now() < end) {
            InputBody in{};
            in.moveX = (rng() % 200 - 100) / 100.f; // -1.0, 1.0 범위 이동 입력
            SendPacket(sock, C2S::INPUT, in);
            std::this_thread::sleep_for(50ms);
        }
    }
};
