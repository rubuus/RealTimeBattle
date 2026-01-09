using System;

[Serializable]
public class PlayerStatePacket : BasePacket
{
    public int userId;
    public float x;
    public float y;
    public string state;
    public int dir;
}
