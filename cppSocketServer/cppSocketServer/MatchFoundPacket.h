#pragma once
#include <cstdint>

enum class Side : uint8_t {
    Left = 0,
    Right = 1
};

#pragma pack(push, 1)

struct MatchFoundPacket {
    int32_t roomId;
    int32_t myUserId;
    int32_t mySessionId;
    int32_t enemyUserId;
    int32_t enemySessionId;
    uint8_t side;

    MatchFoundPacket(int32_t r, int32_t mu, int32_t ms, int32_t eu, int32_t es, Side s)
        : roomId(r), myUserId(mu), mySessionId(ms), enemyUserId(eu), enemySessionId(es), side(static_cast<uint8_t>(s)) {
    }
};

#pragma pack(pop)
static_assert(sizeof(MatchFoundPacket) == 21, "MatchFoundPacket struct size must be 13 bytes");