using System;
using UnityEngine;

[Serializable]
public class MatchFoundPacket : BasePacket
{
    public int roomId;
    public int myId;
    public int enemyId;
    public string side;
}
