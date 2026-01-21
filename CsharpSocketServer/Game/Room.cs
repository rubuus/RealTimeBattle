using System.Collections.Concurrent;
using System.Numerics;
using api.DTOs;

public enum RoomEventType
{
    BattleReady,
    BattleStart,
    PlayerInput,
    ResultAck,
    Disconnect
}

public struct RoomEvent
{
    public RoomEventType EventType { get; init; }
    public int SessionId { get; init; }
    public object? Payload { get; init; }
}

public enum RoomOutEventType
{
    LoadBattle,
    PlayerSpawn,
    StateUpdate,
    TimeUpdate,
    Attack,
    GameResult,
    EnemyExit,
    CloseRoom
}

public struct RoomOutEvent
{
    public RoomOutEventType EventType { get; init; }
    public int SessionId { get; init; }
    public object Payload { get; init; }
}

public readonly struct Box
{
    public float X { get; }
    public float Y { get; }
    public float HalfWidth  { get; }
    public float HalfHeight { get; }

    public Box(float x, float y, float halfW, float halfH)
    {
        X = x;
        Y = y;
        HalfWidth = halfW;
        HalfHeight = halfH;
    }
}

public sealed class Room
{
    public int RoomId { get; }

    public ClientSession Player1 { get; }
    public ClientSession Player2 { get; }

    public ServerPlayer ServerPlayer1 { get; private set; } = null!;
    public ServerPlayer ServerPlayer2 { get; private set; } = null!;

    private static readonly Vector2 LeftSpawn  = new(-7f, -2.3f);
    private static readonly Vector2 RightSpawn = new( 7f, -2.3f);

    private HashSet<int> _readySet = new();
    private bool _gameStarted;
    private bool _battleStarted;

    private int _waitingAckCount = 2;
    private bool _gameEnded;
    private Dictionary<int, bool> _ackReceivedMap = new();
    public bool Closed { get; private set; }
    private bool _closeQueued;

    public float GameTime { get; private set; } = 100.0f;
    public float StateSendAcc { get; private set; } = 0.0f;
    public float TimeSendAcc { get; private set; } = 0.0f;

    private readonly ConcurrentQueue<RoomEvent> _eventQueue = new();
    private readonly ConcurrentQueue<RoomOutEvent> _outEventQueue = new();
    private const int MAX_OUT_EVENTS_PER_TICK = 20;

    public Room(int roomId, ClientSession p1, ClientSession p2)
    {
        RoomId = roomId;
        Player1 = p1;
        Player2 = p2;
        ServerPlayer1 = new ServerPlayer(Player1.UserId, "LEFT", LeftSpawn);
        ServerPlayer2 = new ServerPlayer(Player2.UserId, "RIGHT", RightSpawn);
    }

    public void EnqueueEvent(RoomEvent ev)
    {
        _eventQueue.Enqueue(ev);

        while (_eventQueue.Count > 20)    
            _eventQueue.TryDequeue(out _);
    }

    public void EmitOutEvent(RoomOutEvent ev)
    {
        _outEventQueue.Enqueue(ev);
    }

    private void InputEvents()
    {
        while(_eventQueue.TryDequeue(out var ev))
        {
            switch (ev.EventType)
            {
                case RoomEventType.BattleReady:
                    CheckMatch(ev);
                    break;

                case RoomEventType.BattleStart:
                    PlayerSpawn();
                    break;

                case RoomEventType.PlayerInput:
                    OnInput(ev);
                    break;

                case RoomEventType.ResultAck:
                    OnAckReceived(ev);
                    break;

                case RoomEventType.Disconnect:
                    OnPlayerDisconnect(ev);
                    break;

                default:
                    break;
            }
        }
    }

    private void OutEvents()
    {
        int processed = 0;

        while (processed < MAX_OUT_EVENTS_PER_TICK &&
        _outEventQueue.TryDequeue(out var ev))
        {
            Transport.Dispatch(ev);
            processed++;
        }
    }

    public void Update(float dt)
    {
        InputEvents();
        OutEvents();

        if (!_gameStarted) return;   // 매치 안됐으면 return
        if (Player1 == null || 
        Player2 == null) return;     // 세션 1명이라도 없으면 return
        if (!_battleStarted) return; // 배틀씬 아니면 return

        GameTime -= dt;

        ServerPlayerUpdate(dt);

        StateSendAcc += dt;
        if (StateSendAcc >= 0.033)
            EmitStateUpdate();

        TimeSendAcc += dt;
        if (TimeSendAcc >= 1.0)
            EmitTimeUpdate();

        EmitDamageUpdate();

        if (GameTime <= 0f || 
        ServerPlayer1.CurrentHp <= 0 || 
        ServerPlayer2.CurrentHp <= 0)
        {
            EndGame();
        }
    }

    private void ServerPlayerUpdate(float dt) 
    {
        ServerPlayer1.Update(dt);
        ServerPlayer2.Update(dt);
    }

    void CheckMatch(RoomEvent ev)
    {
        _readySet.Add(ev.SessionId);

        if (_readySet.Count < 2)
            return;

        _gameStarted = true;

        RoomOutEvent p1 = new RoomOutEvent
        {
            EventType = RoomOutEventType.LoadBattle,
            SessionId = Player1.SessionId,
            Payload = new BasePacket { Type = "LOAD_BATTLE" }
        };

        RoomOutEvent p2 = new RoomOutEvent
        {
            EventType = RoomOutEventType.LoadBattle,
            SessionId = Player2.SessionId,
            Payload = new BasePacket { Type = "LOAD_BATTLE" }
        };

        EmitOutEvent(p1);
        EmitOutEvent(p2);
    }

    private void PlayerSpawn()
    {
        if (_battleStarted) return;
        _battleStarted = true;

        var p1State = ServerPlayer1.StatePacket();
        var p2State = ServerPlayer2.StatePacket();

        RoomOutEvent p1 = new RoomOutEvent
        {
            EventType = RoomOutEventType.PlayerSpawn,
            SessionId = Player1.SessionId,
            Payload = p1State
        };

        RoomOutEvent p1p2 = new RoomOutEvent
        {
            EventType = RoomOutEventType.PlayerSpawn,
            SessionId = Player1.SessionId,
            Payload = p2State
        };

        RoomOutEvent p2 = new RoomOutEvent
        {
            EventType = RoomOutEventType.PlayerSpawn,
            SessionId = Player2.SessionId,
            Payload = p1State
        };

        RoomOutEvent p2p1 = new RoomOutEvent
        {
            EventType = RoomOutEventType.PlayerSpawn,
            SessionId = Player2.SessionId,
            Payload = p2State
        };

        EmitOutEvent(p1);
        EmitOutEvent(p1p2);
        EmitOutEvent(p2);
        EmitOutEvent(p2p1);
    }

    private void OnInput(RoomEvent ev)
    {
        if (ev.SessionId == Player1.SessionId)
            ServerPlayer1.ApplyInput((PlayerInputPacket)ev.Payload!);
        else
            ServerPlayer2.ApplyInput((PlayerInputPacket)ev.Payload!);
    }

    private void EmitStateUpdate()
    {
        StateSendAcc = 0.0f;

        if (!ServerPlayer1.IsStateDirty && !ServerPlayer2.IsStateDirty)
            return;

        var p1State = ServerPlayer1.StatePacket();
        var p2State = ServerPlayer2.StatePacket();

        RoomOutEvent p1 = new RoomOutEvent
        {
            EventType = RoomOutEventType.StateUpdate,
            SessionId = Player1.SessionId,
            Payload = p1State
        };

        RoomOutEvent p1p2 = new RoomOutEvent
        {
            EventType = RoomOutEventType.StateUpdate,
            SessionId = Player1.SessionId,
            Payload = p2State
        };

        RoomOutEvent p2 = new RoomOutEvent
        {
            EventType = RoomOutEventType.StateUpdate,
            SessionId = Player2.SessionId,
            Payload = p1State
        };

        RoomOutEvent p2p1 = new RoomOutEvent
        {
            EventType = RoomOutEventType.StateUpdate,
            SessionId = Player2.SessionId,
            Payload = p2State
        };

        EmitOutEvent(p1);
        EmitOutEvent(p1p2);
        EmitOutEvent(p2);
        EmitOutEvent(p2p1);

        ServerPlayer1.ClearStateDirty();
        ServerPlayer2.ClearStateDirty();
    }

    private void EmitDamageUpdate() 
    {
        bool p1Hit = CheckDamage(ServerPlayer1, ServerPlayer2);
        bool p2Hit = CheckDamage(ServerPlayer2, ServerPlayer1);

        if (p1Hit) BroadcastDamage(ServerPlayer2);
        if (p2Hit) BroadcastDamage(ServerPlayer1);
    }

    private void BroadcastDamage(ServerPlayer hurt)
    {
        EmitOutEvent(new RoomOutEvent {
            EventType = RoomOutEventType.Attack,
            SessionId = Player1.SessionId,
            Payload = hurt.HurtPacket()
        });

        EmitOutEvent(new RoomOutEvent {
            EventType = RoomOutEventType.Attack,
            SessionId = Player2.SessionId,
            Payload = hurt.HurtPacket()
        });
    }

    private bool CheckDamage(ServerPlayer atk, ServerPlayer tar)
    {
        // Punch 상태가 아니면 데미지 판정 안 함
        if (atk.State != PlayerState.Punch)
            return false;

        // 이미 판정이 체크된 상태면 데미지 판정 안 함
        if (atk.PunchChecked)
            return false;

        // 펀치 범위에 상대가 없으면 데미지 판정 안 함
        if (!IsInPunchRange(atk, tar))
            return false;

        atk.SetPunchChecked(true);

        // 피격 방향 설정
        if (atk.Position.X > tar.Position.X && tar.Direction < 0)
            tar.SetDir(1);
        else if (atk.Position.X < tar.Position.X && tar.Direction > 0)
            tar.SetDir(-1);

        tar.TakeDamage(10, atk.Direction * 1.0f);

        return true;
    }

    private bool IsInPunchRange(ServerPlayer atk, ServerPlayer tar)
    {
        Box hitbox = new Box
        (
            atk.Position.X + (0.3f * atk.Direction), 
            atk.Position.Y,
            0.3f,
            0.7f
        );

        Box hurtbox = new Box
        (
            tar.Position.X,
            tar.Position.Y + 0.5f,
            0.5f,
            0.5f
        );
        
        return Overlap(hitbox, hurtbox);
    }

    private bool Overlap(Box hit, Box hurt)
    {
        return !(Math.Abs(hit.X - hurt.X) > (hit.HalfWidth + hurt.HalfWidth) ||
                Math.Abs(hit.Y - hurt.Y) > (hit.HalfHeight + hurt.HalfHeight));
    }

    private void EmitTimeUpdate()
    {
        TimeSendAcc -= 1.0f;

        TimeSyncPacket time = new TimeSyncPacket
        {
            Type = "GAME_TIME",
            Time = (int)Math.Ceiling(GameTime)
        };

        RoomOutEvent p1 = new RoomOutEvent
        {
            EventType = RoomOutEventType.TimeUpdate,
            SessionId = Player1.SessionId,
            Payload = time
        };

        RoomOutEvent p2 = new RoomOutEvent
        {
            EventType = RoomOutEventType.TimeUpdate,
            SessionId = Player2.SessionId,
            Payload = time
        };
        
        EmitOutEvent(p1);
        EmitOutEvent(p2);
    }

    public async Task SaveRecordAsync(ClientSession winner, ClientSession loser)
    {
        var req = new SaveRecordRequest
        {
            WinnerId = winner.UserId,
            LoserId = loser.UserId
        };

        bool success = await ApiClient.Post("battle/save", req);

        if (success)
            Console.WriteLine("전적 저장됨");
        else
            Console.WriteLine("전적 저장 실패");
    }

    private void EndGame()
    {
        if (_gameEnded)
            return;

        _gameEnded = true;

        EmitGameResult();
        BeginCloseAckPhase();
    }

    public void EmitGameResult()
    {
        if (ServerPlayer1.CurrentHp > ServerPlayer2.CurrentHp)
        {
            RoomOutEvent p1 = new RoomOutEvent
            {
                EventType = RoomOutEventType.GameResult,
                SessionId = Player1.SessionId,
                Payload = new BasePacket { Type = "GAME_WIN" }
            };

            RoomOutEvent p2 = new RoomOutEvent
            {
                EventType = RoomOutEventType.GameResult,
                SessionId = Player2.SessionId,
                Payload = new BasePacket { Type = "GAME_LOSE" }
            };

            EmitOutEvent(p1);
            EmitOutEvent(p2);

            _ = SaveRecordAsync(Player1, Player2);
        }
        else if (ServerPlayer1.CurrentHp < ServerPlayer2.CurrentHp)
        {
            RoomOutEvent p1 = new RoomOutEvent
            {
                EventType = RoomOutEventType.GameResult,
                SessionId = Player1.SessionId,
                Payload = new BasePacket { Type = "GAME_LOSE" }
            };

            RoomOutEvent p2 = new RoomOutEvent
            {
                EventType = RoomOutEventType.GameResult,
                SessionId = Player2.SessionId,
                Payload = new BasePacket { Type = "GAME_WIN" }
            };

            EmitOutEvent(p1);
            EmitOutEvent(p2);

            _ = SaveRecordAsync(Player2, Player1);
        }
        else
        {
            RoomOutEvent p1 = new RoomOutEvent
            {
                EventType = RoomOutEventType.GameResult,
                SessionId = Player1.SessionId,
                Payload = new BasePacket { Type = "GAME_DRAW" }
            };

            RoomOutEvent p2 = new RoomOutEvent
            {
                EventType = RoomOutEventType.GameResult,
                SessionId = Player2.SessionId,
                Payload = new BasePacket { Type = "GAME_DRAW" }
            };

            EmitOutEvent(p1);
            EmitOutEvent(p2);

            _ = SaveRecordAsync(Player1, Player2);
        }
    }

    private void BeginCloseAckPhase()
    {
        _ackReceivedMap.Clear();
        _ackReceivedMap[Player1.SessionId] = false;
        _ackReceivedMap[Player2.SessionId] = false;

        _waitingAckCount = 2;

        RoomOutEvent p1 = new RoomOutEvent
        {
            EventType = RoomOutEventType.GameResult,
            SessionId = Player1.SessionId,
            Payload = new BasePacket { Type = "ROOM_CLOSED" }
        };

        RoomOutEvent p2 = new RoomOutEvent
        {
            EventType = RoomOutEventType.GameResult,
            SessionId = Player2.SessionId,
            Payload = new BasePacket { Type = "ROOM_CLOSED" }
        };

        EmitOutEvent(p1);
        EmitOutEvent(p2);
    }

    private void OnAckReceived(RoomEvent re)
    {
        if (_ackReceivedMap[re.SessionId]) return;

        _ackReceivedMap[re.SessionId] = true;
        _waitingAckCount--;

        if (_waitingAckCount == 0)
            Closed = true;
    }

    private void OnPlayerDisconnect(RoomEvent re)
    {
        if (Closed) return;
        Closed = true;

        ClientSession? winner = null;
        ClientSession? loser = null;

        if (re.SessionId == Player1.SessionId) {
            winner = Player2;
            loser = Player1;
        }
        else if (re.SessionId == Player2.SessionId) {
            winner = Player1;
            loser = Player2;
        }
        else return;

        RoomOutEvent exit = new RoomOutEvent
        {
            EventType = RoomOutEventType.EnemyExit,
            SessionId = winner.SessionId,
            Payload = new BasePacket { Type = "ENEMY_EXIT" }
        };

        RoomOutEvent close = new RoomOutEvent
        {
            EventType = RoomOutEventType.CloseRoom,
            SessionId = winner.SessionId,
            Payload = new BasePacket { Type = "ROOM_CLOSED" }
        };

        EmitOutEvent(exit);
        EmitOutEvent(close);
        _ = SaveRecordAsync(winner, loser);
    }

    public bool TryQueueClose()
    {
        if (!Closed) return false;
        if (_closeQueued) return false;

        _closeQueued = true;
        return true;
    }

    public void CloseRoom()
    {
        if (Closed) return;

        Player1.Room = null;
        Player2.Room = null;
    }
}
