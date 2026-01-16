#pragma once

struct RoomOutEvent;

class Network 
{
public:
	void Dispatch(const RoomOutEvent& ev);

private:
	void SendReadyRoom(const RoomOutEvent& ev);
	void SendSpawn(const RoomOutEvent& ev);
	void SendState(const RoomOutEvent& ev);
	void SendDamage(const RoomOutEvent& ev);
	void SendTime(const RoomOutEvent& ev);
	void SendResult(const RoomOutEvent& ev);
	void SendEnemyExit(const RoomOutEvent& ev);
	void SendRoomClosed(const RoomOutEvent& ev);
};