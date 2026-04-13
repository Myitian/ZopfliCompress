namespace ZopfliCompress.PNG;

interface IChunk
{
    PNGChuckType ChunkType { get; }
    void WriteTo(Stream stream);
}