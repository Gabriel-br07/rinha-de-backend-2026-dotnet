using System.Buffers;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

static int Main(string[] args)
{
    try
    {
        var input = args.Length > 0 ? args[0] : "resources/references.json.gz";
        var outDir = args.Length > 1 ? args[1] : "data";

        Directory.CreateDirectory(outDir);

        var referencesPath = Path.Combine(outDir, "references.bin");
        var labelsPath = Path.Combine(outDir, "labels.bin");

        using var inputFs = File.OpenRead(input);
        using var gzip = new GZipStream(inputFs, CompressionMode.Decompress, leaveOpen: false);

        using var refsFs = new FileStream(referencesPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 20);
        using var labelsFs = new FileStream(labelsPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 20);

        var count = ConvertReferencesJsonToBinary(gzip, refsFs, labelsFs);

        refsFs.Flush(flushToDisk: true);
        labelsFs.Flush(flushToDisk: true);

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
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        return 1;
    }
}

static long ConvertReferencesJsonToBinary(Stream jsonStream, Stream referencesOut, Stream labelsOut)
{
    const int BufferSize = 1 << 20;
    byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
    byte[] outVec = ArrayPool<byte>.Shared.Rent(14);

    try
    {
        var state = new JsonReaderState(new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var bytesInBuffer = 0;
        var isFinalBlock = false;
        long count = 0;

        bool inArray = false;

        while (true)
        {
            if (bytesInBuffer < BufferSize / 2 && !isFinalBlock)
            {
                var read = jsonStream.Read(buffer, bytesInBuffer, BufferSize - bytesInBuffer);
                if (read == 0) isFinalBlock = true;
                else bytesInBuffer += read;
            }

            var reader = new Utf8JsonReader(new ReadOnlySpan<byte>(buffer, 0, bytesInBuffer), isFinalBlock, state);

            while (reader.Read())
            {
                if (!inArray)
                {
                    if (reader.TokenType != JsonTokenType.StartArray)
                        throw new JsonException("Expected top-level JSON array");
                    inArray = true;
                    continue;
                }

                if (reader.TokenType == JsonTokenType.EndArray)
                    return count;

                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Expected object inside top-level array");

                Span<float> v14 = stackalloc float[14];
                var gotVector = false;
                byte label = 0;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException("Expected property name");

                    var prop = reader.ValueTextEquals("vector"u8) ? 1 :
                               reader.ValueTextEquals("label"u8) ? 2 : 0;

                    reader.Read();

                    if (prop == 1)
                    {
                        if (reader.TokenType != JsonTokenType.StartArray)
                            throw new JsonException("Expected vector array");

                        for (var i = 0; i < 14; i++)
                        {
                            if (!reader.Read())
                                throw new JsonException("Unexpected end while reading vector");

                            if (reader.TokenType != JsonTokenType.Number)
                                throw new JsonException("Vector element must be number");

                            v14[i] = reader.GetSingle();
                        }

                        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
                            throw new JsonException("Vector array must have exactly 14 elements");

                        gotVector = true;
                    }
                    else if (prop == 2)
                    {
                        if (reader.TokenType != JsonTokenType.String)
                            throw new JsonException("Label must be string");

                        if (reader.ValueTextEquals("fraud"u8)) label = 1;
                        else label = 0;
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                if (!gotVector)
                    throw new JsonException("Missing vector property");

                Encode14(v14, outVec);
                referencesOut.Write(outVec, 0, 14);
                labelsOut.WriteByte(label);
                count++;
            }

            state = reader.CurrentState;

            var consumed = (int)reader.BytesConsumed;
            var remaining = bytesInBuffer - consumed;
            if (remaining > 0)
                Buffer.BlockCopy(buffer, consumed, buffer, 0, remaining);
            bytesInBuffer = remaining;

            if (isFinalBlock)
                break;
        }

        throw new JsonException("Unexpected end of JSON");
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
        ArrayPool<byte>.Shared.Return(outVec);
    }
}

static void Encode14(ReadOnlySpan<float> v14, Span<byte> dst14)
{
    for (var i = 0; i < 14; i++)
        dst14[i] = Encode(v14[i]);
}

static byte Encode(float v)
{
    if (v == -1f) return 0;
    if (v <= 0f) return 1;
    if (v >= 1f) return 255;

    var scaled = (int)MathF.Round(v * 254f);
    if (scaled <= 0) return 1;
    if (scaled >= 254) return 255;
    return (byte)(1 + scaled);
}
