using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;

public class ClientSession(int id, TcpClient client, SocketServer server)
{
    public int SessionId { get; set; } = id;
    public int UserId { get; set; }
    public int RoomId;
    public Room ?Room;

    public Vector2 lastPos;
    public string lastState = "Idle";
    public bool isGameEnded = false;
    public bool player1Ended = false;
    public bool player2Ended = false;
    public bool battleReady = false;
    public int currentHp = 100;

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
                if (battleReady)  // battle 준비 완료된 클라만 좌표 처리
                {
                    var p = JsonSerializer.Deserialize<PlayerMovePacket>(msg);
                    Room?.UpdatePlayerState(this, p);
                }
            }

            if (msg.Contains("\"type\":\"HIT\""))
            {
                var p = JsonSerializer.Deserialize<HitPacket>(msg);
                Room?.UpdatePlayerHP(p);
                Console.WriteLine(msg);
            }

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
            
            case "BATTLE_READY":
                battleReady = true;
                break;

            case "GAME_END":
                battleReady = false;
                
                if (Room != null)
                    Room.OnGameEnd(this);
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

        if (Room != null)
            Room.OnPlayerDisconnect(this);

        SocketServer.Instance.RemoveClient(this);

        // 3) 소켓/세션 정리
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
    }

    public void ClearRoom()
    {
        Room = null;
    }
}
