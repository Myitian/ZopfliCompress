using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Reflection;
using ZopfliCompress.PNG;

namespace ZopfliCompress;

sealed class Program
{
    static int Iteration;
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            Console.WriteLine("""

                Entering interactive mode...
                """);
            args = [
                Input("Mode:"),
                Input("Iterations:"),
                Input("Input file path:").AsSpan().Trim().Trim('"').ToString(),
                Input("Output file path:").AsSpan().Trim().Trim('"').ToString()
            ];
        }
        if (args is not [string mode, string iteration, string input, string output])
        {
            mode = "help";
            iteration = "";
            input = "";
            output = "";
        }
        ReadOnlySpan<char> modeSpan = mode.AsSpan().Trim().TrimStart('-');
        if (!int.TryParse(iteration, out Iteration) || Iteration < 0)
        {
            Console.WriteLine($"""
                Invalid iterations: {iteration}
                Iterations must be a non-negative integer.
                Use 'help' for usage information.
                """);
            return;
        }
        if (!File.Exists(input))
        {
            Console.WriteLine($"""
                Input file does not exist: {input}
                Use 'help' for usage information.
                """);
            return;
        }
        switch (modeSpan)
        {
            case "help":
                PrintHelp();
                break;
            case "png":
                if (!PNGHandler(input, output))
                {
                    Console.WriteLine($"""
                        Failed to process PNG file: {input}
                        Ensure the file is a valid PNG and try again.
                        """);
                }
                break;
            case "zip":
                Console.WriteLine("Work in progress");
                break;
            default:
                Console.WriteLine($"""
                    Unknown mode: {mode}
                    Use 'help' for usage information.
                    """);
                break;
        }
    }
    private static void PrintHelp()
    {
        AssemblyName name = Assembly.GetExecutingAssembly().GetName();
        Console.WriteLine($"""
                    {name.Name} v{name.Version?.ToString(2)}

                    Usage: {name.Name} <mode> <iterations> <input> <output>
                    Modes:
                      png - Recompress IDAT chunks in PNG file
                    Iterations:
                      Number of iterations to perform (higher means better compression but slower)
                    """);
    }
    private static string Input(string prompt)
    {
        Console.WriteLine(prompt);
        return Console.ReadLine() ?? "";
    }

    public static string? Zopfli(string file, ZopfliMode mode)
    {
        (string modeArg, string fileExt) = mode switch
        {
            ZopfliMode.Gzip => ("--gzip", ".gz"),
            ZopfliMode.Zlib => ("--zlib", ".zlib"),
            ZopfliMode.Deflate => ("--deflate", ".deflate"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Invalid Zopfli mode")
        };
        Process zopfli = Process.Start("zopfli", [modeArg, $"--i{Iteration}", file]);
        zopfli.WaitForExit();
        return zopfli.ExitCode == 0 ? file + fileExt : null;
    }
    public static bool PNGHandler(string input, string output)
    {
        Console.WriteLine();
        PNGFile? png;
        Console.WriteLine($"""
            Reading PNG from
              {input}
            """);
        using (FileStream fs = File.OpenRead(input))
        {
            if (!PNGFile.TryReadFrom(fs, out png))
                return false;
        }
        string tempPath = Path.Combine(Path.GetTempPath(), nameof(ZopfliCompress), Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(tempPath);
        List<IChunk> tempChunks = DecompressChunks(png, tempPath);
        png.Chunks.Clear();
        foreach (IChunk chunk in tempChunks)
        {
            switch (chunk)
            {
                case ExternalChunk extChunk:
                    switch (extChunk.ChunkType)
                    {
                        case PNGChuckType.IDAT:
                            Console.WriteLine($"Compressing {extChunk.File.Name}");
                            string? result = Zopfli(extChunk.File.FullName, ZopfliMode.Zlib);
                            if (result is null)
                            {
                                Console.WriteLine("Failed to compress IDAT chunk");
                                return false;
                            }
                            extChunk.File = new(result);
                            if (!extChunk.File.Exists)
                            {
                                Console.WriteLine("Unknown error: Compressed file does not exist");
                                return false;
                            }
                            if (extChunk.File.Length <= int.MaxValue)
                                png.Chunks.Add(extChunk);
                            else
                            {
                                long offset = 0;
                                while (offset < extChunk.File.Length)
                                {
                                    png.Chunks.Add(extChunk.WithRange(offset, int.MaxValue));
                                    offset += int.MaxValue;
                                }
                            }
                            break;
                        default:
                            png.Chunks.Add(chunk);
                            break;
                    }
                    break;
                default:
                    png.Chunks.Add(chunk);
                    break;
            }
        }
        if (Path.GetDirectoryName(Path.GetFullPath(output)) is string dir)
            Directory.CreateDirectory(dir);
        Console.WriteLine($"""
            Saving PNG to
              {output}
            """);
        using (FileStream fs = File.Open(output, FileMode.Create, FileAccess.Write, FileShare.Read))
            png.WriteTo(fs);
        Directory.Delete(tempPath, true);
        return true;

        static List<IChunk> DecompressChunks(PNGFile png, string tempPath)
        {
            List<IChunk> tempChunks = [];
            int sequence = -1;
            // -1: non-fdAT
            // >=0: fdAT sequence number
            ExternalChunk? external = null;
            PushBasedDecompressor? decompressor = null;
            try
            {
                foreach (IChunk chunk in png.Chunks)
                {
                    switch (chunk.ChunkType)
                    {
                        case PNGChuckType.IDAT when chunk is StandardChunk stdChunk:
                            if (sequence >= 0)
                            {
                                sequence = -1;
                                decompressor?.Dispose();
                                external = null;
                            }
                            if (external is null || decompressor is null)
                            {
                                decompressor?.Dispose();
                                external = new(new FileInfo(Path.Combine(tempPath, $"{Guid.CreateVersion7()}_IDAT")))
                                {
                                    ChunkType = PNGChuckType.IDAT
                                };
                                tempChunks.Add(external);
                                Console.WriteLine($"""
                                    Extracing IDAT chunk data to
                                      {external.File.FullName}
                                    """);
                                decompressor = new(
                                    stream => new ZLibStream(stream, CompressionMode.Decompress),
                                    external.File.Open(FileMode.Create, FileAccess.Write, FileShare.Read));
                            }
                            decompressor.OnDataReceived(stdChunk.ChunkData);
                            break;
                        case PNGChuckType.iCCP: // prefixed:
                        // - Profile name           1 - 79 bytes(character string)
                        // - Null separator         1 byte(null character)
                        // - Compression method     1 byte
                        case PNGChuckType.zTXt: // prefixed:
                        // - Keyword                1 - 79 bytes(character string)
                        // - Null separator         1 byte(null character)
                        // - Compression method     1 byte
                        case PNGChuckType.iTXt: // prefixed:
                        // - Keyword                1 - 79 bytes(character string)
                        // - Null separator         1 byte(null character)
                        // - Compression flag       1 byte
                        // - Compression method     1 byte
                        // - Language tag           0 or more bytes(character string)
                        // - Null separator         1 byte(null character)
                        // - Translated keyword     0 or more bytes
                        // - Null separator         1 byte(null character)
                        case PNGChuckType.fdAT: // prefixed: sequence_number    4 bytes

                            // TODO: iCCP/zTXt/iTXt/fdAT
                            Console.WriteLine($"Work in progress: {chunk.ChunkType.GetName()} support");
                            goto default;
                        default:
                            sequence = -1;
                            decompressor?.Dispose();
                            external = null;
                            tempChunks.Add(chunk);
                            break;
                    }
                }
                return tempChunks;
            }
            finally
            {
                decompressor?.Dispose();
            }
        }
    }
    class PushBasedDecompressor : IDisposable
    {
        private readonly Pipe pipe = new();
        private readonly Func<Stream, Stream> decompressorFactory;
        private readonly Stream destination;

        public Task Task { get; }
        public PushBasedDecompressor(Func<Stream, Stream> decompressorFactory, Stream destination)
        {
            this.decompressorFactory = decompressorFactory;
            this.destination = destination;
            Task = Task.Run(StartDecompressing);
        }
        public void OnDataReceived(ReadOnlySpan<byte> data)
        {
            pipe.Writer.Write(data);
        }
        public void OnComplete()
        {
            pipe.Writer.Complete();
        }
        private async Task StartDecompressing()
        {
            using Stream decompressor = decompressorFactory(pipe.Reader.AsStream());
            byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                int bytesRead;
                while ((bytesRead = await decompressor.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void Dispose()
        {
            OnComplete();
            Task.Wait();
            destination.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}