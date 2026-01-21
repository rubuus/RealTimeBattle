using System;

[Serializable]
public class PlayerStatePacket : BasePacket
{
    public int UserId;
    public float X;
    public float Y;
    public string State;
    public short Dir;
}
