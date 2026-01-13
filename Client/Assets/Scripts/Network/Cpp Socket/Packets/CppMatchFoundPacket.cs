using System;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CppMatchFoundPacket
{
    public int roomId;
    public int myUserId;
    public int mySessionId;
    public int enemyUserId;
    public int enemySessionId;
    public sbyte side;
}
