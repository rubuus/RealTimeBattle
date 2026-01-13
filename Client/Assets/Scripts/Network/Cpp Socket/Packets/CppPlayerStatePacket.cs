using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CppPlayerStatePacket
{
    public int playerId;
    public float x;
    public float y;
    public byte state;
    public sbyte dir;
}
