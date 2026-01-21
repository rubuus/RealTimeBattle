using System;

[Serializable]
public class DamagePacket : BasePacket
{
    public int HurtId;
    public int CurrentHp;
}