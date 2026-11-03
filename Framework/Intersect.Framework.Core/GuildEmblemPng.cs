using System.Buffers.Binary;
using System.IO.Compression;
namespace Intersect.Framework.Core;

/// <summary>Bounds PNG decoding and removes ancillary data before publishing a guild upload.</summary>
public static class GuildEmblemPng
{
    public const int MaximumBytes = 65536;
    public static byte[] Sanitize(byte[] source)
    {
        if (source == null || source.Length is < 45 or > MaximumBytes || !source.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) throw new InvalidDataException();
        using var result = new MemoryStream(); result.Write(source, 0, 8);
        using var compressed = new MemoryStream();
        int offset = 8, width = 0, height = 0, channels = 0; bool ended = false, seenData = false;
        while (offset + 12 <= source.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));
            if (length < 0 || length > source.Length - offset - 12) throw new InvalidDataException();
            var type = System.Text.Encoding.ASCII.GetString(source, offset + 4, 4);
            var data = source.AsSpan(offset + 8, length);
            if (Crc(source.AsSpan(offset + 4, length + 4)) != BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(offset + 8 + length, 4))) throw new InvalidDataException();
            if (offset == 8 && type != "IHDR") throw new InvalidDataException();
            if (type == "IHDR")
            {
                if (offset != 8 || length != 13) throw new InvalidDataException();
                width = BinaryPrimitives.ReadInt32BigEndian(data); height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                if (width is < 1 or > 128 || height is < 1 or > 128 || data[8] != 8 || data[9] is not (2 or 6) || data[10] != 0 || data[11] != 0 || data[12] != 0) throw new InvalidDataException();
                channels = data[9] == 6 ? 4 : 3;
            }
            else if (type == "IDAT") { if (ended) throw new InvalidDataException(); compressed.Write(data); seenData = true; }
            else if (type == "IEND") { if (length != 0 || !seenData) throw new InvalidDataException(); ended = true; }
            else if (char.IsUpper(type[0]) && type != "PLTE") throw new InvalidDataException();
            if (type is "IHDR" or "IDAT" or "IEND") result.Write(source, offset, length + 12);
            offset += length + 12;
            if (ended) break;
        }
        if (!ended || offset != source.Length || channels == 0) throw new InvalidDataException();
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var row = new byte[1 + width * channels];
        for (var y = 0; y < height; y++) { zlib.ReadExactly(row); if (row[0] > 4) throw new InvalidDataException(); }
        if (zlib.ReadByte() != -1) throw new InvalidDataException();
        return result.ToArray();
    }
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        uint value = uint.MaxValue;
        foreach (var b in data) { value ^= b; for (int bit = 0; bit < 8; bit++) value = (value >> 1) ^ (0xedb88320u & (uint)-(int)(value & 1)); }
        return ~value;
    }
}
