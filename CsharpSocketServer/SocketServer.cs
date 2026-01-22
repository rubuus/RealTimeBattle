using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

/*
SocketServer.cs

역할 :
- 서버 관련 쓰레드 모음

*/

public class SocketServer
{
    public static SocketServer Instance { get; private set; } = null!;

    public bool running { get; private set; } = true;

    private TcpListener _listener = null!;
    private int _port;
    public string SecretKey { get; } = "V8rG#b3Yp0!tQs7Wk9@Zx2&Nm5eUj4Ha";

    private readonly ConcurrentDictionary<int, ClientSession> _clients = new();

    private readonly LinkedList<ClientSession> _matchList = new();
    private readonly object _matchLock = new();

    private readonly Dictionary<int, Room> _rooms = new();
    private readonly object _roomLock = new();
    private int _roomIdCounter = 1;

    private readonly ConcurrentQueue<int> _closedRoom = new();


    public SocketServer(int port)
    {
        if (Instance != null)
            throw new InvalidOperationException("SocketServer already created");
            
        Instance = this;
        _port = port;
    }

    public async Task StartServer()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        Console.WriteLine("Server Started");

        // TickLoop 시작 (서버 전체에서 딱 1개만)
        _ = TickLoop();
        _ = HeartbeatLoop();

        int clientId = 1;

        while (running)
        {
            var tcpClient = await _listener.AcceptTcpClientAsync();
            tcpClient.NoDelay = true;
            tcpClient.Client.NoDelay = true;

            var session = new ClientSession(clientId, tcpClient);
            _clients[clientId] = session;
            Console.WriteLine($"[SERVER] Client {clientId} Connected");

            _ = session.ReceiveLoop();
            clientId++;
        }
    }
    
    // 게임 로직 루프
    private async Task TickLoop()
    {
        const int TICK_RATE = 60; // 60 FPS
        const long TICK_MS = 1000 / TICK_RATE;
        var sw = Stopwatch.StartNew();
        long nextTick = sw.ElapsedMilliseconds;

        while (running)
        {
            long now = sw.ElapsedMilliseconds;

            if (now >= nextTick)
            {
                float dt = 1f / TICK_RATE;
                List<Room> rooms;

                lock (_roomLock)
                {
                    rooms = _rooms.Values.ToList();
                }

                foreach (var room in rooms)
                {
                    room.Update(dt);

                    if (room.TryQueueClose())
                        _closedRoom.Enqueue(room.RoomId);
                }

                ProcessRoomClosed();
                nextTick = now + TICK_MS;
            }
            else
            {
                int delay = (int)(nextTick - now);
                if (delay > 2)
                    await Task.Delay(delay - 1);
            }
        }
    }

    // 1초 주기로 각각 세션에 연결 확인
    private async Task HeartbeatLoop()
    {
        while (running)
        {
            List<ClientSession> timeoutList = new();
            List<ClientSession> aliveList = new();

            foreach (var s in _clients.Values)
            {
                if (s == null || s.IsDisconnected)
                    continue;

                TimeSpan duration = DateTime.UtcNow - s.LastPingTime;

                // 5초 이상 응답 없을 시, time out
                if (duration.TotalSeconds > 5)
                    timeoutList.Add(s);
                else
                    aliveList.Add(s);
            }
            
            // 타임 아웃 된 세션은 연결 종료
            foreach (var s in timeoutList)
            {
                s.Disconnect($"{s.SessionId} session is timeout");
                RemoveClient(s);
            }

            // 살아있는 세션에 응답
            foreach (var s in aliveList)
            {
                s.Send(new BasePacket { Type = "PONG" });
            }

            await Task.Delay(1000);
        }
    }

    // 매칭 큐
    public void AddToMatchList(ClientSession s)
    {
        ClientSession? p1, p2;

        // 동시 접근 보호
        lock (_matchLock)
        {
            if (_matchList.Contains(s))
                return;

            _matchList.AddLast(s);
            Console.WriteLine($"[MATCH] User {s.SessionId} Enqueued");

            if (_matchList.Count < 2) return;

            p1 = _matchList.First!.Value; _matchList.RemoveFirst();
            p2 = _matchList.First!.Value; _matchList.RemoveFirst();
        }
        
        if (p1.IsDisconnected || p2.IsDisconnected)
        {
            lock (_matchLock)
            {
                if (!p1.IsDisconnected) _matchList.AddFirst(p1);
                if (!p2.IsDisconnected) _matchList.AddFirst(p2);
            }
            return;
        }

        CreateRoom(p1, p2);
    }

    private void CreateRoom(ClientSession p1, ClientSession p2)
    {
        // 한 클라에서 매칭 2번으로 룸 생성 방지
        if (p1 == p2 || p1.SessionId == p2.SessionId)
            return;

        int roomId = _roomIdCounter++;

        Room r;

        // 동시 접근 보호
        lock (_roomLock)
        {
            r = new Room(roomId, p1, p2);
            _rooms.Add(roomId, r);

            p1.Room = r;
            p2.Room = r;
        }

        Console.WriteLine($"[ROOM] Room {roomId} Created ({p1.UserId}, {p2.UserId})");

        NotifyMatchFound(roomId, p1, p2);
    }

    private void NotifyMatchFound(int rid, ClientSession p1, ClientSession p2)
    {
        p1.Send(new MatchFoundPacket
        {
            Type = "MATCH_FOUND",
            RoomId = rid,
            MyUserId = p1.UserId,
            MySessionId = p1.SessionId,
            EnemyUserId = p2.UserId,
            EnemySessionId = p2.SessionId,
            Side = "LEFT",
        });

        p2.Send(new MatchFoundPacket
        {
            Type = "MATCH_FOUND",
            RoomId = rid,
            MyUserId = p2.UserId,
            MySessionId = p2.SessionId,
            EnemyUserId = p1.UserId,
            EnemySessionId = p1.SessionId,
            Side = "RIGHT",
        });
    }

    public bool RoomAlive(ClientSession s)
    {
        lock (_roomLock)
        {
            if (!_rooms.TryGetValue(s.Room!.RoomId, out _))
                return false;

            return true;
        }
    }

    public ClientSession? FindSession(int sid)
    {
        if (!_clients.TryGetValue(sid, out var session))
            return null;
        else return session;
    }

    private void ProcessRoomClosed()
    {
        while (_closedRoom.TryDequeue(out var roomId))
            CloseRoom(roomId);
    }

    private void CloseRoom(int roomId)
    {
        Room dying;

        // 소유권 이동 후, room 컨테이너에서 삭제
        lock (_roomLock) 
        {
            if (!_rooms.TryGetValue(roomId, out _)) return;
            dying = _rooms[roomId];
            _rooms.Remove(roomId);
        }
        
        // 락 밖에서 룸 정리
        dying.CloseRoom();
        Console.WriteLine($"[ROOM] Room {roomId} Deleted");
    }

    private void RemoveClient(ClientSession s)
    {
        Console.WriteLine($"[REMOVE] sid={s.SessionId} reason=RemoveClient");
        // 매칭리스트에서 세션 제거
        lock (_matchLock)
        {
            _matchList.Remove(s);
        }

        ClientSession? dying = null;

        // 세션 정리할 준비가 되면 소유권 넘긴 후, clients 컨테이너에서 제거
        if (!_clients.TryGetValue(s.SessionId, out dying))
            return;

        if (!dying.CanCleanUp())
            return;

        _clients.TryRemove(s.SessionId, out _);

        dying.Disconnect("Remove Client");
        Console.WriteLine($"[SERVER] Client {s.SessionId} removed completely.");
    }
}