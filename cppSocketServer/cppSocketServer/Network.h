#pragma once
#include "RoomEvent.h"

class Network 
{
public:
	void Dispatch(const RoomOutEvent& ev);

private:
	void BroadcastReadyRoom(const RoomOutEvent& ev);
	void BroadcastState(const RoomOutEvent& ev);
	void BroadcastDamage(const RoomOutEvent& ev);
	void BroadcastTime(const RoomOutEvent& ev);
	void BroadcastResult(const RoomOutEvent& ev);
	void BroadcastEnemyExit(const RoomOutEvent& ev);
	void BroadcastRoomClosed(const RoomOutEvent& ev);
};