using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;

public class ClientSession(int id, TcpClient client, SocketServer server)
{
    public int sessionId { get; set; } = id;
    public int userId { get; set; }
    public int roomId;

    public Vector2 lastPos;
    public string lastState = "Idle";
    private bool disconnected = false;
    public bool player1Ended = false;
    public bool player2Ended = false;
    public bool battleReady = false;
    public int currentHp = 100;

    public Room? _room;
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
        if (disconnected)
            return;

        try
        {
            var basePacket = JsonSerializer.Deserialize<BasePacket>(msg);

            switch (basePacket?.type)
            {
                case "LOGIN":
                    {
                        var p = JsonSerializer.Deserialize<LoginPacket>(msg);
                        userId = p.userId;
                    }
                    break;

                case "MATCH_START":
                    _server.AddToMatchQueue(this);
                    break;
                
                case "BATTLE_READY":
                    _room.CheckMatch(this);
                    break;
                
                case "BATTLE_START":
                    battleReady = true;
                    break;

                case "INPUT":
                    if (battleReady)
                    {
                        var p = JsonSerializer.Deserialize<PlayerInputPacket>(msg);
                        _room.OnInputPacket(this, p);
                    }
                    break;

                /*case "HIT":
                    {
                        var p = JsonSerializer.Deserialize<HitPacket>(msg);
                        _room.UpdatePlayerHP(p);
                    }
                    break;*/

                case "GAME_END":
                    battleReady = false;
                    
                    if (_room != null)
                        _room.OnGameEnd(this);
                    break;

                default:
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void Send(object obj)
    {
        if (!_client.Connected) return;

        string json = JsonSerializer.Serialize(obj);
        byte[] data = Encoding.UTF8.GetBytes(json + "\n");
        _stream.Write(data, 0, data.Length);
    }

    public void Disconnect()
    {
        Console.WriteLine($"[DISCONNECT] {sessionId}");

        disconnected = true;

        // 1) 스트림 먼저 닫기 (ReceiveLoop 강제 종료)
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }

        // 2) 룸 정리
        if (_room != null)
            _room.OnPlayerDisconnect(this);

        // 3) 클라이언트 목록 제거
        SocketServer.Instance.RemoveClient(this);
    }
}
