public class PlayerMovePacket : BasePacket
{
    public int id { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public string state { get; set; } = string.Empty;
}
