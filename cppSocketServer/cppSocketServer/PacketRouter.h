#pragma once


class ClientSession;
class Room;

class PacketRouter
{
public:
	static void RoutePacket(ClientSession& sender, char* data, int length);

private:
};

