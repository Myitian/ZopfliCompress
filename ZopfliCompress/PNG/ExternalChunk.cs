using System.Buffers;
using System.Buffers.Binary;
using System.IO.Hashing;

namespace ZopfliCompress.PNG;

sealed class ExternalChunk(FileInfo file) : IChunk
{
    public PNGChuckType ChunkType { get; set; }
    public FileInfo File { get; set; } = file;
    public long Offset { get; set; }
    public long Length { get; set; } = long.MaxValue;
    public byte[] Prefix { get; set; } = [];
    public void WriteTo(Stream stream)
    {
        Crc32 crc = new();
        Span<byte> buffer = stackalloc byte[8];
        long length = File.Exists ? Math.Max(Math.Min(File.Length - Offset, Length), 0) : 0;
        BinaryPrimitives.WriteInt32BigEndian(buffer, checked((int)(Prefix.Length + length)));
        BinaryPrimitives.WriteUInt32BigEndian(buffer[4..], (uint)ChunkType);
        stream.Write(buffer);
        crc.Append(buffer[4..]);
        stream.Write(Prefix);
        crc.Append(Prefix);
        if (length > 0)
        {
            using FileStream fs = File.OpenRead();
            fs.Position = Offset;
            CopyAndAppend(fs, stream, crc, Length);
        }
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc.GetCurrentHashAsUInt32());
        stream.Write(buffer[..4]);
    }
    private static void CopyAndAppend(Stream source, Stream destination, Crc32 crc, long lengthLimit)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            while (true)
            {
                long maxRead = Math.Min(lengthLimit, buffer.Length);
                int bytesRead = source.Read(buffer, 0, (int)maxRead);
                if (bytesRead <= 0)
                    break;
                crc.Append(buffer.AsSpan(0, bytesRead));
                destination.Write(buffer, 0, bytesRead);
                lengthLimit -= bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    public ExternalChunk WithRange(long offset, long length)
    {
        return new ExternalChunk(File)
        {
            ChunkType = ChunkType,
            Offset = offset,
            Length = length,
            Prefix = Prefix
        };
    }
}