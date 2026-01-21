#pragma once
#include <cstdint>
#include <variant>
#include "PlayerInputPacket.h"
#include "PlayerStatePacket.h"
#include "TimeSyncPacket.h"
#include "DamagePacket.h"

enum class RoomEventType : uint8_t
{
    BattleReady,
    BattleStart,
    PlayerInput,
    ResultAck,
    Disconnect
};

enum class RoomOutEventType : uint8_t
{
    LoadBattle,
    PlayerSpawn,
	StateUpdate,
	TimeUpdate,
    Attack,
    GameResult,
	EnemyExit,
    CloseRoom
};

struct UpdateStatePayload
{
    PlayerStatePacket p1;
    PlayerStatePacket p2;
};

struct UpdateTimePayload
{
    TimeSyncPacket time;
};

struct UpdateHurtPayload
{
    DamagePacket dmg;
};

struct GameResultPayload
{
    int winner;
};

struct GameEndPayload
{
    int winner;
};

// Room -> Transport 출력 이벤트 payload
// 보내는 패킷은 variant로 감싸서 안전하게 type 체크
using RoomOutPayload = std::variant<
    UpdateStatePayload,
    UpdateTimePayload,
    UpdateHurtPayload,
    GameResultPayload,
    GameEndPayload
>;

struct RoomEvent
{
    RoomEventType type;
    int sessionId;

    // Transport -> Room 입력 이벤트
    // POD 타입 + 즉시 소비 전제라 union 사용 가능
    union
    {
        PlayerInputPacket input;
        PlayerStatePacket state;
        DamagePacket damage;
    };
};

struct RoomOutEvent
{
    RoomOutEventType type;
    int sessionId;
	RoomOutPayload payload;
};