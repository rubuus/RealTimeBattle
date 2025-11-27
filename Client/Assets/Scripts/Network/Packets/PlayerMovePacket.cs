using System;

[Serializable]
public class PlayerMovePacket : BasePacket
{
    public int id;
    public float x;
    public float y;
    public string state;
}
