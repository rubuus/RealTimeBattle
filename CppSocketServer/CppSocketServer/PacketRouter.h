#pragma once
#include <unordered_set>
#include "ClientSession.h"

class ClientSession;

struct ParsedPacket {
	uint16_t type;
    const char* body;
    uint16_t bodySize;
};

class PacketRouter {
public:
    static PacketRouter& Instance();

    void Route(ClientSession& session, const ParsedPacket& pkt);

	void HandleLogin(ClientSession& s, const char* body, uint16_t bodySize);
	void HandleMatchStart(ClientSession& s);
	void HandelMatchCancel(ClientSession& s);
	void HandleBattleReady(ClientSession& s);
	void HandleBattleStart(ClientSession& s);
	void HandleInput(ClientSession& s, const char* body, uint16_t bodySize);
	void HandleResultAck(ClientSession& s);
	void HandlePing(ClientSession& s);
	void HandleLoginTest(ClientSession& s, const char* body, uint16_t bodySize);

	std::unordered_set<int> onlineUsers;
};

