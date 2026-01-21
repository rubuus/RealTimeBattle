
using Newtonsoft.Json;

public class Transport
{
    public static void Dispatch(RoomOutEvent ev)
    {
        var session = SocketServer.Instance.FindSession(ev.SessionId);
        
        // 이벤트 처리 시점에 세션이 이미 종료되었을 수 있으므로 null 체크
        if (session == null)
        {
        Console.WriteLine($"[DISPATCH] session {ev.SessionId} NOT FOUND. keys={string.Join(",", SocketServer.Instance.DebugClientKeys())
        }");
        }
        Console.WriteLine("[DISPATCH] ok " + ev.EventType);
        switch (ev.EventType)
        {
            case RoomOutEventType.LoadBattle:
                SendReadyRoom(ev);
                break;

            case RoomOutEventType.PlayerSpawn:
                SendSpawn(ev);
                break;

            case RoomOutEventType.StateUpdate:
                SendState(ev);
                break;

            case RoomOutEventType.TimeUpdate:
                SendTime(ev);
                break;

            case RoomOutEventType.Attack:
                SendDamage(ev);
                break;

            case RoomOutEventType.GameResult:
                SendResult(ev);
                break;

            case RoomOutEventType.EnemyExit:
                SendEnemyExit(ev);
                break;

            case RoomOutEventType.CloseRoom:
                SendRoomClosed(ev);
                break;

            default:
                break;
        }
    }

    // 룸 생성 시, 해당 세션에 패킷 전송
    private static void SendReadyRoom(RoomOutEvent ev)
    {
        var s = SocketServer.Instance.FindSession(ev.SessionId);
        if (s == null) return;

        s.Send(ev.Payload);
    }

    // 해당 세션에 스폰용으로 상태 패킷 한번 전송
    private static void SendSpawn(RoomOutEvent ev)
    {
        var s = SocketServer.Instance.FindSession(ev.SessionId);
        if (s == null) return;

        s.Send(ev.Payload);
    }

    // 해당 세션에 상태 패킷 전송
    private static void SendState(RoomOutEvent ev)
    {
        var s = SocketServer.Instance.FindSession(ev.SessionId);
        if (s == null) return;

        s.Send(ev.Payload);
    }

    // 해당 세션에 시간 패킷 전송
    private static void SendTime(RoomOutEvent ev)
    {
        var s = SocketServer.Instance.FindSession(ev.SessionId);
        if (s == null) return;

        s.Send(ev.Payload);
    }

    // 해당 세션에 데미지 패킷 전송
    private static void SendDamage(RoomOutEvent ev)
    {
        var s = SocketServer.Instance.FindSession(ev.SessionId);
        if (s == null) return;

        s.Send(ev.Payload);
    }

    // 해당 세션에 결과 패킷 한번 전송
    private static void SendResult(RoomOutEvent ev)
    {
        var s = SocketServer.Instance.FindSession(ev.SessionId);
        if (s == null) return;

        s.Send(ev.Payload);
    }

    // 해당 세션에 상대 종료 패킷 전송
    private static void SendEnemyExit(RoomOutEvent ev)
    {
        var s = SocketServer.Instance.FindSession(ev.SessionId);
        if (s == null) return;

        s.Send(ev.Payload);
    }

    // 해당 세션에 룸 닫힘 패킷 전송
    private static void SendRoomClosed(RoomOutEvent ev)
    {
        var s = SocketServer.Instance.FindSession(ev.SessionId);
        if (s == null) return;

        s.Send(ev.Payload);
    }
}