using System.Numerics;

public class Room
{
    public int RoomId;
    public ClientSession Player1;
    public ClientSession Player2;

    public Room(int roomId, ClientSession p1, ClientSession p2)
    {
        RoomId = roomId;
        Player1 = p1;
        Player2 = p2;

        p1.Room = this;
        p2.Room = this;
    }

    // 좌표 및 행동 Sync
    public void BroadcastToOther(int senderId, string json)
    {
        if (Player1.SessionId != senderId)
            Player1.Send(json);

        if (Player2.SessionId != senderId)
            Player2.Send(json);
    }

    public void UpdatePlayerState(ClientSession sender, PlayerMovePacket p)
    {
        // sender가 Player1인지 Player2인지 확인
        if (sender == Player1)
        {
            Player1.lastPos = new Vector2(p.x, p.y);
            Player1.lastState = p.state;
            Player1.lastDir = p.dir;
        }
        else
        {
            Player2.lastPos = new Vector2(p.x, p.y);
            Player2.lastState = p.state;
            Player2.lastDir = p.dir;
        }
    }

    public void CloseRoom()
    {
        Player1.Room = null;
        Player2.Room = null;
    }
}
