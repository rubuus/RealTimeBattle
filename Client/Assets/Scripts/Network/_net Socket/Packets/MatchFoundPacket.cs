using System;
using UnityEngine;

[Serializable]
public class MatchFoundPacket : BasePacket
{
    public int roomId;
    public int mySessionId;
    public int enemySessionId;
    public string side;
}
