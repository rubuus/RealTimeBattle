public class DamagePacket : BasePacket
{
    public int id { get; set; }
    public int amount { get; set; }
    public int currentHP { get; set; }
}