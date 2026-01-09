using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CppPlayerInputPacket
{
    public int id;
    public float move;
    public byte jump;
    public byte dash;
    public byte punch;
}
