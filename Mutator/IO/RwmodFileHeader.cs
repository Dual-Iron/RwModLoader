namespace Mutator.IO;

// TODO upgrade this format
// rwmod file format: https://gist.github.com/Dual-Iron/b28590195548cb382874f0040ec96b78
sealed class RwmodFileHeader
{
    public const int EntryCountByteOffset = 4;

    public static RwmodFileHeader Read(Stream stream)
    {
        using BinaryReader reader = new(stream, ExtIO.Enc, true);

        byte flags = reader.ReadByte();
        RwmodVersion version = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        ushort entryCount = reader.ReadUInt16();
        string name = reader.ReadString();
        string author = reader.ReadString();
        string home = reader.ReadString();
        string displayName = reader.ReadString();

        return new(name, author) {
            Flags = flags,
            DisplayName = displayName,
            Homepage = home,
            EntryCount = entryCount,
            ModVersion = version
        };
    }

    public RwmodFileHeader(string name, string author)
    {
        Name = name;
        Author = author;
        DisplayName = name;
    }

    public byte Flags;
    public RwmodVersion ModVersion;
    public ushort EntryCount;
    public string Name;
    public string Author;
    public string DisplayName;
    public string Homepage = "";

    public bool IsRepo => Homepage.StartsWith("https://github.com/");

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream, ExtIO.Enc, true);

        writer.Write(Flags);
        writer.Write((byte)ModVersion.Major);
        writer.Write((byte)ModVersion.Minor);
        writer.Write((byte)ModVersion.Patch);
        writer.Write(EntryCount);
        writer.Write(Name);
        writer.Write(Author);
        writer.Write(Homepage);
        writer.Write(DisplayName);
    }
}
