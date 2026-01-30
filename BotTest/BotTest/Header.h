#pragma once
#include <cstdint>

#pragma pack(push, 1)
struct PacketHeader {
    uint16_t type;
    uint16_t size;
};
#pragma pack(pop)

#pragma pack(push, 1)
struct LoginBody {
    int32_t userId;
};
#pragma pack(pop)

#pragma pack(push, 1)
struct InputBody {
    float moveX;
    uint8_t jump;
    uint8_t dash;
    uint8_t punch;
};
#pragma pack(pop)

enum class C2S : uint16_t {
    LOGIN = 0,
    MATCH_START = 1,
    MATCH_CANCEL = 2,
    BATTLE_READY = 3,
    BATTLE_START = 4,
    INPUT = 5,
    RESULT_ACK = 6,
    PING = 7,
    LOGIN_TEST = 8
};

enum class S2C : uint16_t {
    MATCH_FOUND = 0,
    LOAD_BATTLE = 1,
    PLAYER_STATE = 2,
    TAKE_DAMAGE = 3,
    GAME_TIME = 4,
    GAME_WIN = 5,
    GAME_LOSE = 6,
    GAME_DRAW = 7,
    ENEMY_EXIT = 8,
    ROOM_CLOSED = 9,
    PONG = 10
};