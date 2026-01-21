using System;
using UnityEngine;

[Serializable]
public class MatchFoundPacket : BasePacket
{
    public int RoomId;
    public int MyUserId;
    public int MySessionId;
    public int EnemyUserId;
    public int EnemySessionId;
    public string Side;
}
