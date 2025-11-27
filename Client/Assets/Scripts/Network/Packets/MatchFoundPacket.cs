using System;
using UnityEngine;

[Serializable]
public class MatchFoundPacket : BasePacket
{
    public int roomId;
    public int myUserId;
    public int enemyUserId;
    public string side;
}
