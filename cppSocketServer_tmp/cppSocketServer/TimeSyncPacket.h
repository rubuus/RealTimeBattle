#pragma once

#pragma pack(push, 1)
struct TimeSyncPacket
{
	int32_t time;
};
#pragma pack(pop)

static_assert(sizeof(TimeSyncPacket) == 4, "TimeSyncPacket struct size must be 4 bytes");