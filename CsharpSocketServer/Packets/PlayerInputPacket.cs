public class PlayerInputPacket : BasePacket
{
    public int id { get; set; }
    public float move { get; set; }
    public bool jump { get; set; }
    public bool dash { get; set; }
    public bool punch { get; set; }
}
