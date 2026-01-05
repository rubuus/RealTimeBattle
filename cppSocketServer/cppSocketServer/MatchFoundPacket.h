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
    int32_t enemyUserId;
    uint8_t side; // Changed from Side to uint8_t

    MatchFoundPacket(int32_t r, int32_t a, int32_t b, Side s)
        : roomId(r), myUserId(a), enemyUserId(b), side(static_cast<uint8_t>(s)) {
    }
};

#pragma pack(pop)
static_assert(sizeof(MatchFoundPacket) == 13, "MatchFoundPacket struct size must be 13 bytes");