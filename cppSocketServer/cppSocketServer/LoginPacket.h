#pragma once
#include <cstdint>
#pragma pack(push, 1)

struct LoginPacket
{
	int32_t userId;
};

#pragma pack(pop)

static_assert(sizeof(LoginPacket) == 4, "LoginPacket struct size must be 4 bytes");