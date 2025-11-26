public class DamagePacket
{
    public string type { get; set; } = string.Empty;
    public int id { get; set; }
    public int amount { get; set; }
    public int currentHP { get; set; }
}