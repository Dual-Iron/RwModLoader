namespace Mutator.IO;

static class RwmodOperations
{
    // Returns the number of unread bytes left over
    public static long CopyStream(Stream input, Stream output, long bytes)
    {
        byte[] buffer = new byte[Math.Min(bytes, 32768)];
        int read;
        while (bytes > 0 && (read = input.Read(buffer.AsSpan(0, (int)Math.Min(bytes, buffer.Length)))) > 0) {
            output.Write(buffer.AsSpan(0, read));
            bytes -= read;
        }
        return bytes;
    }

    public static void ReadRwmodEntries(RwmodFileHeader header, Stream rwmod, Func<string, Stream> handleEntry)
    {
        using BinaryReader reader = new(rwmod, ExtIO.Enc, true);

        for (int i = 0; i < header.EntryCount; i++) {
            long size = reader.ReadInt64();
            string name = reader.ReadString();

            using Stream outputFile = handleEntry(name);

            long bytesLeft = CopyStream(rwmod, outputFile, size);

            if (bytesLeft != 0) {
                throw new("Corrupt rwmod");
            }
        }
    }

    public static void WriteRwmodEntry(Stream rwmod, RwmodEntry entry)
    {
        using BinaryWriter writer = new(rwmod, ExtIO.Enc, true);

        // Filesize
        writer.Write(entry.Contents.Length);

        // Filename
        writer.Write(entry.FileName);

        CopyStream(entry.Contents, rwmod, entry.Contents.Length);
    }

    public struct RwmodEntry
    {
        public string FileName;
        public Stream Contents;
    }
}