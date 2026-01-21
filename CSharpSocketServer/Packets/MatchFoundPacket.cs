public class MatchFoundPacket : BasePacket
{
    public int RoomId { get; set; }
    public int MyUserId { get; set; }
    public int MySessionId { get; set; }
    public int EnemyUserId { get; set; }
    public int EnemySessionId { get; set; }
    public string Side { get; set; } = string.Empty;
}
