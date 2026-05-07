using System.Buffers;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FraudDetection.Preprocess.Ivf;

namespace FraudDetection.Preprocess;

internal static class Program
{
    private sealed class ReferenceDto
    {
        public float[]? Vector { get; set; }
        public string? Label { get; set; }
    }

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var input = args.Length > 0 ? args[0] : "resources/references.json.gz";
            var outDir = args.Length > 1 ? args[1] : "data";
            var nlist = args.Length > 2 ? int.Parse(args[2]) : 1024;

            Directory.CreateDirectory(outDir);

            var referencesPath = Path.Combine(outDir, "references.bin");
            var labelsPath = Path.Combine(outDir, "labels.bin");

            using var inputFs = File.OpenRead(input);
            using var gzip = new GZipStream(inputFs, CompressionMode.Decompress, leaveOpen: false);

            // We keep preprocessing deterministic and simple: write exact binaries, then build IVF from them.
            await using (var refsFs = new FileStream(referencesPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 20, useAsync: true))
            await using (var labelsFs = new FileStream(labelsPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 20, useAsync: true))
            {
                var count = await ConvertReferencesJsonToBinaryAsync(gzip, refsFs, labelsFs);

                await refsFs.FlushAsync();
                await labelsFs.FlushAsync();

                var refsLen = refsFs.Length;
                var labelsLen = labelsFs.Length;

                if (refsLen % 14 != 0)
                    throw new InvalidDataException($"references.bin size {refsLen} is not divisible by 14");

                var refsCount = refsLen / 14;
                if (labelsLen != refsCount)
                    throw new InvalidDataException($"labels.bin size {labelsLen} doesn't match vector count {refsCount}");

                if (count != refsCount)
                    throw new InvalidDataException($"written vector count mismatch: parser={count} file={refsCount}");

                Console.WriteLine($"OK: wrote {count} references to '{referencesPath}' and '{labelsPath}'");
            }

            // Build IVF-Flat index (random centroids MVP).
            var vectors = File.ReadAllBytes(referencesPath);
            var labels = File.ReadAllBytes(labelsPath);

            IvfIndexBuilder.Build(
                vectors,
                labels,
                nlist,
                out var centroids,
                out var offsets,
                out var groupedVectors,
                out var groupedLabels,
                seed: 42);

            IvfIndexWriter.WriteAll(outDir, nlist, centroids, offsets, groupedVectors, groupedLabels);
            Console.WriteLine($"OK: wrote IVF index (nlist={nlist}) to '{outDir}'");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static async Task<long> ConvertReferencesJsonToBinaryAsync(Stream jsonStream, Stream referencesOut, Stream labelsOut)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        byte[] outVec = ArrayPool<byte>.Shared.Rent(14);
        try
        {
            long count = 0;

            await foreach (var dto in JsonSerializer.DeserializeAsyncEnumerable<ReferenceDto>(jsonStream, options))
            {
                if (dto is null)
                    continue;

                if (dto.Vector is null || dto.Vector.Length != 14)
                    throw new JsonException("Vector must have exactly 14 elements");

                Encode14(dto.Vector, outVec);
                await referencesOut.WriteAsync(outVec, 0, 14);

                var label = string.Equals(dto.Label, "fraud", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)0;
                labelsOut.WriteByte(label);

                count++;
            }

            return count;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(outVec);
        }
    }

    private static void Encode14(ReadOnlySpan<float> v14, Span<byte> dst14)
{
    for (var i = 0; i < 14; i++)
        dst14[i] = Encode(v14[i]);
    }

    private static byte Encode(float v)
{
    if (v == -1f) return 0;
    if (v <= 0f) return 1;
    if (v >= 1f) return 255;

    var scaled = (int)MathF.Round(v * 254f);
    if (scaled <= 0) return 1;
    if (scaled >= 254) return 255;
    return (byte)(1 + scaled);
    }
}
