public class PlayerStatePacket : BasePacket
{
    public int UserId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public string State { get; set; } = string.Empty;
    public short Dir { get; set; }
}
