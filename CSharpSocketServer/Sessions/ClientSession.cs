using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

public class ClientSession(int sid, TcpClient tcp)
{
    public bool IsAuthenticated { get; private set; } = false;
    public int SessionId { get; } = sid;
    public int UserId { get; set; }

    public bool IsDisconnected { get; private set; } = false;
    public bool PendingIo { get; private set; }

    public bool BattleReady { get; private set; }
    public bool AckReceived { get; private set; }

    public Room? Room { get; set; }
    public NetworkStream Stream { get; } = tcp.GetStream();
    private readonly object _sendLock = new();

    public DateTime LastPingTime { get; set; } = DateTime.UtcNow;

    private readonly byte[] _recvBuffer = new byte[4096];

    public void SetAuth(bool b)
    {
        IsAuthenticated = b;
    }

    public async Task ReceiveLoop()
    {
        var textBuffer = new StringBuilder();
        
        try
        {
            while (!IsDisconnected)
            {
                int read = await Stream.ReadAsync(_recvBuffer, 0, _recvBuffer.Length);
                if (read == 0)
                {
                    Console.WriteLine($"[Session {SessionId}] remote closed (FIN)");
                    break;
                }

                textBuffer.Append(Encoding.UTF8.GetString(_recvBuffer, 0, read));

                // 받은 패킷을 모두 소모
                while (true)
                {
                    string current = textBuffer.ToString();
                    int newlineIndex = current.IndexOf('\n');
                    if (newlineIndex < 0)
                        break;

                    string packet = textBuffer.ToString(0, newlineIndex).TrimEnd('\r');
                    textBuffer.Remove(0, newlineIndex + 1);

                    PacketRouter.Route(this, packet);
                }
            }
        }
        catch (IOException e)
        {
            Console.WriteLine(e);
        }
        catch (ObjectDisposedException e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            Disconnect("Session Disconnect");
        }
    }

    public void Send(object obj)
    {
        if (IsDisconnected) return;
        if (obj == null) return;

        var json = JsonConvert.SerializeObject(obj, Formatting.None);
        var data = Encoding.UTF8.GetBytes(json + "\n");

        try
        {
            lock (_sendLock)
            {
                Stream.Write(data, 0, data.Length);
            }
        }
        catch (Exception e)
        {
            Disconnect($"Send Exception : {e}");
        }
    }

    public bool CanCleanUp()
    {
        if (!PendingIo && !IsDisconnected) 
            return true;
        return false;
    }

    public void Disconnect(string reason)
    {
        Console.WriteLine($"[DISCONNECT] sid={SessionId} reason={reason}");

        PacketRouter.OnlineUsers.Remove(UserId);

        if (IsDisconnected) return;
        IsDisconnected = true;

        // 1) 스트림 먼저 닫기 (ReceiveLoop 강제 종료)
        try { Stream?.Close(); } catch { }
        try { tcp?.Close(); } catch { }

        // 2) 룸 정리 이벤트 넘겨주기
        if (Room != null)
            Room.EnqueueEvent(new RoomEvent
            {
                EventType = RoomEventType.Disconnect,
                SessionId = this.SessionId,
                Payload = null
            });
    }
}
