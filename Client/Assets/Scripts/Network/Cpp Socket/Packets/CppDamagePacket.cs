using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CppDamagePacket
{
    public int hurtId;
    public int currentHP;
}