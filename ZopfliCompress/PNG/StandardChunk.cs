using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;

namespace ZopfliCompress.PNG;

sealed class StandardChunk : IChunk
{
    public int Length => ChunkData.Length;
    public PNGChuckType ChunkType { get; set; }
    public byte[] ChunkData { get; set; } = [];
    public uint CRC
    {
        get
        {
            Crc32 crc = new();
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)ChunkType);
            crc.Append(buffer);
            crc.Append(ChunkData);
            return crc.GetCurrentHashAsUInt32();
        }
    }
    public static bool TryReadFrom(Stream stream, [NotNullWhen(true)] out StandardChunk? chunk, bool checkCRC = false)
    {
        Span<byte> buffer = stackalloc byte[8];
        if (stream.ReadAtLeast(buffer, 8, false) < 8)
        {
            chunk = null;
            return false;
        }
        uint length = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        if (length > (uint)Array.MaxLength)
        {
            chunk = null;
            return false;
        }
        chunk = new()
        {
            ChunkType = (PNGChuckType)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]),
            ChunkData = new byte[length]
        };
        if (stream.ReadAtLeast(chunk.ChunkData, chunk.ChunkData.Length, false) < chunk.ChunkData.Length)
            return false;
        if (stream.ReadAtLeast(buffer[..4], 4, false) < 4)
            return false;
        if (checkCRC && chunk.CRC != BinaryPrimitives.ReadUInt32BigEndian(buffer))
            return false;
        return true;
    }
    public void WriteTo(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(buffer, Length);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[4..], (uint)ChunkType);
        stream.Write(buffer);
        stream.Write(ChunkData);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, CRC);
        stream.Write(buffer[..4]);
    }
}