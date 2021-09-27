namespace Realm.ModLoading;

public sealed class RwmodFileHeader
{
    // rwmod file format: https://gist.github.com/Dual-Iron/35b71cdd5ffad8b5ad65a3f7214af390
    public RwmodFileHeader(Stream input)
    {
        BinaryReader reader = new(input, Encoding.ASCII);

        Flags = (RwmodFlags)reader.ReadByte();
        ModVersion = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        EntryCount = reader.ReadUInt16();
        Name = reader.ReadString();
        Author = reader.ReadString();
        Homepage = reader.ReadString();
        DisplayName = reader.ReadString();
    }

    public readonly RwmodFlags Flags;
    public readonly RwmodVersion ModVersion;
    public readonly ushort EntryCount;
    public readonly string Name;
    public readonly string Author;
    public readonly string Homepage;
    public readonly string DisplayName;

    public bool Enabled => ProgramState.Current.Prefs.EnabledMods.Contains(Name);
}
