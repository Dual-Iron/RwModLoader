namespace Rwml.IO;

// RWMOD file format: https://gist.github.com/Dual-Iron/b28590195548cb382874f0040ec96b78
readonly struct RwmodFileEntry
{
    public readonly string Name;
    public readonly uint Size;
    public readonly long Offset;

    public RwmodFileEntry(string name, uint size, long offset = -1)
    {
        Name = name;
        Offset = offset;
        Size = size;
    }

    public static IEnumerable<RwmodFileEntry> ReadAll(Stream strm)
    {
        while (strm.Position < strm.Length) {
            byte[] buffer = new byte[20];

            var name = RwmodIO.ReadStringFull(ref buffer, strm);
            var size = RwmodIO.ReadUInt32(ref buffer, strm);
            var offset = strm.Position;
            
            strm.Position += size;

            yield return new(name, size, offset);
        }
    }

    public void Write(Stream strm)
    {
        RwmodIO.WriteStringFull(strm, Name);
        RwmodIO.WriteUInt32(strm, Size);
    }
}
