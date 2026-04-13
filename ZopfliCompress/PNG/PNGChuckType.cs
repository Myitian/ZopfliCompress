namespace ZopfliCompress.PNG;

enum PNGChuckType : uint
{
    /// <summary>Image header</summary>
    IHDR = 0x49484452,
    /// <summary>Palette</summary>
    PLTE = 0x504C5445,
    /// <summary>Image data</summary>
    IDAT = 0x49444154,
    /// <summary>Image trailer</summary>
    IEND = 0x49454E44,

    /// <summary>Embedded ICC profile</summary>
    iCCP = 0x69434350,
    /// <summary>Compressed textual data</summary>
    zTXt = 0x7A545874,
    /// <summary>International textual data</summary>
    iTXt = 0x69545874,
    /// <summary>Frame data chunk</summary>
    fdAT = 0x66644154,
}
static class PNGChuckTypeExtensions
{
    public static string GetName(this PNGChuckType type)
    {
        uint value = (uint)type;
        ReadOnlySpan<char> chars = [
            (char)(value >> 24),
            (char)((value >> 16) & 0xFF),
            (char)((value >> 8) & 0xFF),
            (char)(value & 0xFF),];
        return new(chars);
    }
}