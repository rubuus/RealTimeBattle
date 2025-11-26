using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text.Json;

public class SocketServer
{
    public static SocketServer Instance;
    private TcpListener _listener;

    private Dictionary<int, ClientSession> _clients = new();
    private Queue<ClientSession> _matchQueue = new();
    private Dictionary<int, Room> _rooms = new();
    private int _roomIdCounter = 1;
    private int _port;

    public SocketServer(int port)
    {
        _port = port;
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        Console.WriteLine("Server Started");

        // ★ TickLoop 시작 (서버 전체에서 딱 1개만)
        _ = TickLoop();

        int clientId = 1;

        while (true)
        {
            var tcpClient = await _listener.AcceptTcpClientAsync();
            var session = new ClientSession(clientId, tcpClient, this);

            _clients.Add(clientId, session);
            Console.WriteLine($"[SERVER] Client {clientId} Connected");

            _ = session.ReceiveLoop();
            clientId++;
        }
    }
    

    // -----------------------
    // 매칭 큐
    // -----------------------
    public void AddToMatchQueue(ClientSession s)
    {
        if (_matchQueue.Contains(s))
            return;

        _matchQueue.Enqueue(s);
        Console.WriteLine($"[MATCH] User {s.UserId} Enqueued");

        if (_matchQueue.Count >= 2)
        {
            var p1 = _matchQueue.Dequeue();
            var p2 = _matchQueue.Dequeue();

            CreateRoom(p1, p2);
        }
    }

    // -----------------------
    // 룸 생성
    // -----------------------
    public void CreateRoom(ClientSession p1, ClientSession p2)
    {
        int roomId = _roomIdCounter++;
        var room = new Room(roomId, p1, p2);
        _rooms.Add(roomId, room);

        Console.WriteLine($"[ROOM] Room {roomId} Created ({p1.UserId}, {p2.UserId})");

        p1.Send($"MATCH_FOUND|{roomId}|{p1.UserId}|{p2.UserId}|LEFT");
        p2.Send($"MATCH_FOUND|{roomId}|{p2.UserId}|{p1.UserId}|RIGHT");
    }

    public void CloseRoom(int roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            room.CloseRoom();
            _rooms.Remove(roomId);
            Console.WriteLine($"[ROOM] Room {roomId} Deleted");
        }
    }

    public Room GetRoom(int roomId)
    {
        return _rooms.TryGetValue(roomId, out var r) ? r : null;
    }

    private async Task TickLoop()
    {
        const int TICK_RATE = 30; // 30 FPS
        const int TICK_DELAY = 1000 / TICK_RATE;

        while (true)
        {
            BroadcastAllRooms();
            await Task.Delay(TICK_DELAY);
        }
    }

    private void BroadcastAllRooms()
    {
        foreach (var room in _rooms.Values)
        {
            BroadcastRoom(room);
        }
    }   

    private void BroadcastRoom(Room room)
    {
        if (!room.Player1.battleReady || !room.Player2.battleReady)
            return;

        SendPlayerState(room.Player1, room.Player2);
        SendPlayerState(room.Player2, room.Player1);
    }

    private void SendPlayerState(ClientSession src, ClientSession dest)
    {
        if (src == null || dest == null) return;

        var pkt = new PlayerMovePacket
        {
            type = "PLAYER_MOVE",
            id = src.SessionId,
            x = src.lastPos.X,
            y = src.lastPos.Y,
            state = src.lastState
        };

        string json = JsonSerializer.Serialize(pkt);
        dest.Send(json);
    }

    public void RemoveClient(ClientSession s)
    {
        // 1) 해당 세션을 매칭큐에서 제거
        _matchQueue = new Queue<ClientSession>(_matchQueue.Where(p => p.SessionId != s.SessionId));

        // 2) 딕셔너리에서 제거
        if (_clients.ContainsKey(s.SessionId))
            _clients.Remove(s.SessionId);

        Console.WriteLine($"[SERVER] Client {s.SessionId} removed completely.");
    }

}