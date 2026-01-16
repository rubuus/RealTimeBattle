using UnityEngine;

public enum C2S_PacketType
{
    LOGIN = 0,
    MATCH_START = 1,
    BATTLE_READY = 2,
    BATTLE_START = 3,
    INPUT = 4,
    RESULT_ACK = 5,
    PING = 6
};

public enum S2C_PacketType
{
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
}