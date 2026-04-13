using System.Diagnostics.CodeAnalysis;

namespace ZopfliCompress.PNG;

class PNGFile
{
    public static ReadOnlySpan<byte> Header => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    public List<IChunk> Chunks { get; set; } = [];
    public static bool TryReadFrom(Stream stream, [NotNullWhen(true)] out PNGFile? png, bool checkHeader = true, bool checkCRC = true)
    {
        Span<byte> header = stackalloc byte[Header.Length];
        if (stream.ReadAtLeast(header, header.Length, false) < header.Length
            || (checkHeader && !header.SequenceEqual(Header)))
        {
            png = null;
            return false;
        }
        png = new();
        StandardChunk? chunk;
        do
        {
            if (!StandardChunk.TryReadFrom(stream, out chunk, checkCRC))
            {
                if (chunk is null)
                {
                    Console.WriteLine("Unexpected end of file");
                }
                else
                {
                    Console.WriteLine($"Found corrupted chunk {chunk.ChunkType.GetName()}");
                    png.Chunks.Add(chunk);
                }
                return false;
            }
            png.Chunks.Add(chunk);
        }
        while (chunk.ChunkType != PNGChuckType.IEND);
        return true;
    }
    public void WriteTo(Stream stream)
    {
        stream.Write(Header);
        foreach (IChunk chunk in Chunks)
            chunk.WriteTo(stream);
    }
}