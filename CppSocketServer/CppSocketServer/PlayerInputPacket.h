#pragma once
#pragma pack(push, 1)
struct PlayerInputPacket
{
    int32_t id;
    float move;
    uint8_t jump;
    uint8_t dash;
    uint8_t punch;
};
#pragma pack(pop)

static_assert(sizeof(PlayerInputPacket) == 11, "PlayerInputPacket struct size must be 14 bytes");