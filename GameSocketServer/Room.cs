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

    public void CloseRoom()
    {
        Player1.Room = null;
        Player2.Room = null;
    }
}
