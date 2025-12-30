#pragma once
#pragma pack(push, 1)
struct PlayerStatePacket
{
    int32_t userId;
    float x;
    float y;
    uint8_t state;
    int8_t dir;
};
#pragma pack(pop)

static_assert(sizeof(PlayerStatePacket) == 14, "PlayerStatePacket struct size must be 14 bytes");