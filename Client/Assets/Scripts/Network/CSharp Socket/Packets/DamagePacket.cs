using System;

[Serializable]
public class DamagePacket : BasePacket
{
    public int hurtId;
    public int currentHP;
}