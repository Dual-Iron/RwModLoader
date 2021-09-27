namespace Mutator.IO;

// rwmod file format: https://gist.github.com/Dual-Iron/35b71cdd5ffad8b5ad65a3f7214af390
public sealed class RwmodFileHeader
{
    public const int EntryCountByteOffset = 4;

    public static RwmodFileHeader Read(Stream stream)
    {
        using BinaryReader reader = new(stream, UseEncoding, true);

        byte flags = reader.ReadByte();
        RwmodVersion version = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        ushort entryCount = reader.ReadUInt16();
        string name = reader.ReadString();
        string author = reader.ReadString();
        string home = reader.ReadString();
        string displayName = reader.ReadString();

        return new(name, author) {
            Flags = (RwmodFlags)flags,
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
    }

    public RwmodFlags Flags;
    public RwmodVersion ModVersion;
    public ushort EntryCount;
    public string Name;
    public string Author;
    public string DisplayName = "";
    public string Homepage = "";

    public bool IsRepo => Homepage.StartsWith("https://github.com/");

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream, UseEncoding, true);

        writer.Write((byte)Flags);
        writer.Write(ModVersion.Major);
        writer.Write(ModVersion.Minor);
        writer.Write(ModVersion.Patch);
        writer.Write(EntryCount);
        writer.Write(Name);
        writer.Write(Author);
        writer.Write(Homepage);
        writer.Write(DisplayName);
    }
}
