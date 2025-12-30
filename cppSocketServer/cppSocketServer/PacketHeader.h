#pragma once
#include <cstdint>

enum class C2S_PacketType : uint16_t{
	LOGIN = 0,
	MATCH_START = 1,
	BATTLE_READY = 2,
	BATTLE_START = 3,
	INPUT = 4,
	RESULT_ACK = 5,
	PONG = 6
};

enum class S2C_PacketType : uint16_t {
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
	PING = 10
};

#pragma pack(push, 1)

struct PacketHeader
{
	uint16_t type;
	uint16_t size;
};

#pragma pack(pop)