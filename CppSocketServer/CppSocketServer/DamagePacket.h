#pragma once

#pragma pack(push, 1)
struct DamagePacket
{
    int32_t hurtId;
    int32_t currentHP;
};
#pragma pack(pop)

static_assert(sizeof(DamagePacket) == 8, "DamagePacket struct size must be 8 bytes");
