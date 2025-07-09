namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct BITMAPV5HEADER
{
    public uint bV5Size;
    public int bV5Width;
    public int bV5Height;
    public ushort bV5Planes;
    public ushort bV5BitCount;
    public uint bV5Compression;
    public uint bV5SizeImage;
    public int bV5XPelsPerMeter;
    public int bV5YPelsPerMeter;
    public ushort bV5ClrUsed;
    public ushort bV5ClrImportant;
    public ushort bV5RedMask;
    public ushort bV5GreenMask;
    public ushort bV5BlueMask;
    public ushort bV5AlphaMask;
    public ushort bV5CSType;
    public nint bV5Endpoints;
    public ushort bV5GammaRed;
    public ushort bV5GammaGreen;
    public ushort bV5GammaBlue;
    public ushort bV5Intent;
    public ushort bV5ProfileData;
    public ushort bV5ProfileSize;
    public ushort bV5Reserved;
}