namespace Mutator.IO;

public static class RwmodOperations
{
    // Note: Using `long` limits this to approx 9 exabytes per file. Not that it really matters.
    // Returns the number of unread bytes left over
    public static async Task<long> CopyStream(Stream input, Stream output, long bytes)
    {
        byte[] buffer = new byte[Math.Min(bytes, 32768)];
        int read;
        while (bytes > 0 && (read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(bytes, buffer.Length)))) > 0) {
            await output.WriteAsync(buffer.AsMemory(0, read));
            bytes -= read;
        }
        return bytes;
    }

    public static async Task ReadRwmodEntries(RwmodFileHeader header, Stream rwmod, Func<string, Stream> handleEntry)
    {
        using BinaryReader reader = new(rwmod, UseEncoding, true);

        for (int i = 0; i < header.EntryCount; i++) {
            long size = reader.ReadInt64();
            string name = reader.ReadString();

            using Stream outputFile = handleEntry(name);

            long bytesLeft = await CopyStream(rwmod, outputFile, size);

            if (bytesLeft != 0) {
                throw Err(ExitCodes.CorruptRwmod);
            }
        }
    }

    public static async Task WriteRwmodEntry(Stream rwmod, RwmodEntry entry)
    {
        using BinaryWriter writer = new(rwmod, UseEncoding, true);

        // Filesize
        writer.Write(entry.Contents.Length);

        // Filename
        writer.Write(entry.FileName);

        await CopyStream(entry.Contents, rwmod, entry.Contents.Length);
    }

    public struct RwmodEntry
    {
        public string FileName;
        public Stream Contents;
    }
}