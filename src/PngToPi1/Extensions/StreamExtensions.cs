using System.IO;

namespace PngToPi1.Extensions;

public static class StreamExtensions
{
    public static void WriteBigEndian16Bits(this Stream stream, ushort value)
    {
        stream.WriteByte((byte)((value & 0xff00) >> 8));
        stream.WriteByte((byte)(value & 0xff));
    }
}