using System;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CppMatchFoundPacket
{
    public int roomId;
    public int myId;
    public int enemyId;
    public sbyte side;
}
