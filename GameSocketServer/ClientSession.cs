using System.Net.Sockets;
using System.Text;

public class ClientSession(int id, TcpClient client, SocketServer server)
{
    public int SessionId { get; set; } = id;
    public int UserId { get; set; }
    public int RoomId;
    public Room Room;

    private TcpClient _client = client;
    private NetworkStream _stream = client.GetStream();
    private SocketServer _server = server;

    private StringBuilder recvBuffer = new StringBuilder();

    public async Task ReceiveLoop()
    {
        byte[] buffer = new byte[1024];
        
        while (true)
        {
            int read = await _stream.ReadAsync(buffer, 0, buffer.Length);
            if (read <= 0) break;

            string text = Encoding.UTF8.GetString(buffer, 0, read);
            recvBuffer.Append(text);

            while (true)
            {
                int idx = recvBuffer.ToString().IndexOf('\n');
                if (idx < 0) break;

                string packet = recvBuffer.ToString(0, idx).Trim();
                recvBuffer.Remove(0, idx + 1);

                HandleMessage(packet);
            }
        }

        Disconnect();
    }


    private void HandleMessage(string msg)
    {
        if (msg.StartsWith("{"))
        {
            // 너무 정교하게 할 필요 없고, type 문자열만 확인
            if (msg.Contains("\"type\":\"PLAYER_MOVE\""))
            {
                // 나 빼고 상대에게 브로드캐스트
                Room?.BroadcastToOther(SessionId, msg);
                return;
            }

            // 나중에 다른 JSON 타입 생기면 여기 else if 추가
            return;
        }

        string command = msg.Contains("|") ? msg.Split('|')[0] : msg;

        switch (command)
        {
            case "LOGIN":
                HandleLogin(msg);
                break;

            case "MATCH_START":
                _server.AddToMatchQueue(this);
                break;

            case "GAME_END":
                if (Room != null)
                    _server.CloseRoom(Room.RoomId);

                Disconnect();
                break;

            case "GAME_EXIT":
                if (Room != null)
                    _server.CloseRoom(Room.RoomId);
                    
                Disconnect();
                break;

            default:
                Console.WriteLine($"[WARN] Unknown command: {msg}");
                break;
        }
}

    public void HandleLogin(string msg)
    {
        string[] data = msg.Split('|');
        int userId = int.Parse(data[1]);

        UserId = userId;

        Console.WriteLine($"[LOGIN] Session {SessionId} mapped to User {UserId}");
    }

    public void Send(string message)
    {
        if (!_client.Connected) return;

        byte[] data = Encoding.UTF8.GetBytes(message + "\n");
        _stream.Write(data, 0, data.Length);
    }

    public void Disconnect()
    {
        Console.WriteLine($"[DISCONNECT] {SessionId}");

        // 1) 방이 있으면 정리 + 상대에게 ENEMY_EXIT 전송
        if (Room != null)
        {
            // 상대에게 알림
            var other = Room.Player1 == this ? Room.Player2 : Room.Player1;

            if (other != null)
                other.Send("ENEMY_EXIT");

            _server.CloseRoom(Room.RoomId);  // 반드시 Room 삭제!
            Room = null;
        }

        // 2) 매칭 큐에서 제거
        _server.RemoveFromMatchQueue(this);

        // 3) 소켓/세션 정리
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
    }
}
