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

    public async Task StartAsync()
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (_client.Connected)
            {
                int read = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) { Disconnect(); break; }

                string msg = Encoding.UTF8.GetString(buffer, 0, read);
                Console.WriteLine($"[RECV][{SessionId}] {msg}");

                HandleMessage(msg);
            }
        }
        catch
        {
            Disconnect();
        }
    }

    private void HandleMessage(string msg)
    {

    string command = msg.Contains("|") ? msg.Split('|')[0] : msg;

    switch (command)
    {
        case "LOGIN":
            HandleLogin(msg);
            break;

        case "MATCH_START":
            _server.AddToMatchQueue(this);
            break;

        case "PLAYER_MOVE":
            Room?.BroadcastToOther(SessionId, msg);
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

        byte[] data = Encoding.UTF8.GetBytes(message);
        _stream.Write(data, 0, data.Length);
    }

    public void Disconnect()
    {
        Console.WriteLine($"[DISCONNECT] {SessionId}");
        _stream.Close();
        _client.Close();
        
        _server.RemoveFromMatchQueue(this);
    }
}
