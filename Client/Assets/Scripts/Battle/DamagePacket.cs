using System;

[Serializable]
public class DamagePacket
{
    public string type;
    public int id;        // 피해자 ID
    public int amount;    // 데미지량
    public int currentHP; // 새 HP (이건 있어도 되고 없어도 됨)
}