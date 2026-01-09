using System;

[Serializable]
public class DamagePacket : BasePacket
{
    public int hurtId;        // 피해자 ID
    public int currentHP; // 새 HP (이건 있어도 되고 없어도 됨)
}