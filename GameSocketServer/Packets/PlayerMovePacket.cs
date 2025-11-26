public class PlayerMovePacket
{
    public string type { get; set; } = string.Empty;
    public int id { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public string state { get; set; } = string.Empty;
}
