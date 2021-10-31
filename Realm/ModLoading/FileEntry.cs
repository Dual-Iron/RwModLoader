namespace Realm.ModLoading;

sealed class FileEntry
{
    public static FileEntry[] GetFileEntries(RwmodFileHeader header, Stream rwmodStream)
    {
        FileEntry[] entries = new FileEntry[header.EntryCount];

        BinaryReader reader = new(rwmodStream, Encoding.ASCII);

        for (int i = 0; i < header.EntryCount; i++) {
            long size = reader.ReadInt64();
            string name = reader.ReadString();

            entries[i] = new(i, name, rwmodStream.Position, size);

            rwmodStream.Position += size;
        }

        return entries;
    }

    public readonly int Index;
    public readonly string FileName;
    public readonly long Offset;
    public readonly long Length;

    public FileEntry(int index, string fileName, long offset, long length)
    {
        Index = index;
        FileName = fileName;
        Offset = offset;
        Length = length;
    }

    public Stream GetStreamSplice(Stream rwmodStream)
    {
        return new SpliceStream(rwmodStream, Offset, Length);
    }
}
