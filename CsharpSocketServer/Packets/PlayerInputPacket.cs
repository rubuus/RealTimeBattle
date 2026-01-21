public class PlayerInputPacket : BasePacket
{
    public int Id { get; set; }
    public float Move { get; set; }
    public bool Jump { get; set; }
    public bool Dash { get; set; }
    public bool Punch { get; set; }
}
