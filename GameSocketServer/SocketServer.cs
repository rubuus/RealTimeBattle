using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

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
        _ = HeartbeatLoop();

        int clientId = 1;

        while (true)
        {
            var tcpClient = await _listener.AcceptTcpClientAsync();
            tcpClient.NoDelay = true;
            tcpClient.Client.NoDelay = true;
            var session = new ClientSession(clientId, tcpClient, this);

            _clients.Add(clientId, session);
            Console.WriteLine($"[SERVER] Client {clientId} Connected");

            _ = session.ReceiveLoop();
            clientId++;
        }
    }
    
    private async Task HeartbeatLoop()
    {
        while (true)
        {
            foreach (var session in _clients.Values)
            {
                try
                {
                    session.Send(new { type = "PING" });
                }
                catch { }
            }

            await Task.Delay(1000);
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
        Console.WriteLine($"[MATCH] User {s.userId} Enqueued");

        if (_matchQueue.Count >= 2)
        {
            var p1 = _matchQueue.Dequeue();
            var p2 = _matchQueue.Dequeue();

            CreateRoom(p1, p2);
        }
    }

    public void CreateRoom(ClientSession p1, ClientSession p2)
    {
        int roomId = _roomIdCounter++;
        var room = new Room(roomId, p1, p2);
        _rooms.Add(roomId, room);

        Console.WriteLine($"[ROOM] Room {roomId} Created ({p1.userId}, {p2.userId})");

        p1.Send(new
        {
            type = "MATCH_FOUND",
            roomId = roomId,
            myId = p1.userId,
            enemyId = p2.userId,
            side = "LEFT",
        });

        p2.Send(new
        {
            type = "MATCH_FOUND",
            roomId = roomId,
            myId = p2.userId,
            enemyId = p1.userId,
            side = "RIGHT",
        });
    }

    public void CloseRoom(int roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return;

        room.CloseRoom();
        _rooms.Remove(roomId);
        Console.WriteLine($"[ROOM] Room {roomId} Deleted");
    }

    private async Task TickLoop()
    {
        const int TICK_RATE = 120; // 120 FPS
        const int TICK_DELAY = 1000 / TICK_RATE;
        float dt = 1f / TICK_RATE;

        while (true)
        {
            foreach (var room in _rooms.Values)
                room.Update(dt);

            CheckHeartbeat();
            await Task.Delay(TICK_DELAY);
        }
    }

    private void CheckHeartbeat()
    {
        DateTime now = DateTime.Now;

        foreach (var session in _clients.Values.ToList())
        {
            if (session.disconnected)
                continue;

            if ((now - session.lastPongTime).TotalSeconds > 5)
            {
                Console.WriteLine($"[HEARTBEAT] Client {session.sessionId} timeout");
                session.Disconnect();   // 🔥 강제로 끊어버림
            }
        }
    }

    public void RemoveClient(ClientSession s)
    {
        // 1) 해당 세션을 매칭큐에서 제거
        _matchQueue = new Queue<ClientSession>(_matchQueue.Where(p => p.sessionId != s.sessionId));

        // 2) 딕셔너리에서 제거
        if (_clients.ContainsKey(s.sessionId))
            _clients.Remove(s.sessionId);

        Console.WriteLine($"[SERVER] Client {s.sessionId} removed completely.");
    }

}