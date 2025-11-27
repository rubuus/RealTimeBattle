using System;
using UnityEngine;

[Serializable]
public class HitPacket : BasePacket
{
    public int hitId;
    public int hurtId;
    public int damage;
}
