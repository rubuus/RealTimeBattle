public class PlayerMovePacket
{
    public string type { get; set; }
    public int id { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public int dir { get; set; }
    public string state { get; set; }
}
