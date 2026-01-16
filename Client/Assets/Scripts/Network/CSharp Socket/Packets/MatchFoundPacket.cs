using System;
using UnityEngine;

[Serializable]
public class MatchFoundPacket : BasePacket
{
    public int roomId;
    public int myUserId;
    public int mySessionId;
    public int enemyUserId;
    public int enemySessionId;
    public string side;
}
