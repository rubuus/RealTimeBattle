public class PlayerStatePacket : BasePacket
{
    public int userId { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public string state { get; set; } = string.Empty;
    public int dir { get; set; }
}
