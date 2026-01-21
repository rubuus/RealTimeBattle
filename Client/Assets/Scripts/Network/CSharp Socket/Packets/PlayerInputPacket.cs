using UnityEngine;

public class PlayerInputPacket : BasePacket
{
    public int Id;
    public float Move;
    public bool Jump;
    public bool Dash;
    public bool Punch;
}
