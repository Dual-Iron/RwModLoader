namespace Realm.ModLoading;

sealed class RwmodFileHeader
{
    public static IEnumerable<RwmodFileHeader> GetRwmodHeaders()
    {
        foreach (var file in RwmodFile.GetRwmodFilePaths()) {
            RwmodFileHeader ret;

            using (Stream s = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                ret = new(file, s);

            yield return ret;
        }
    }

    public RwmodFileHeader(string filePath, Stream input)
    {
        BinaryReader reader = new(input, Encoding.ASCII);

        Flags = reader.ReadByte();
        ModVersion = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        EntryCount = reader.ReadUInt16();
        Name = reader.ReadString();
        Author = reader.ReadString();
        Homepage = reader.ReadString();
        DisplayName = reader.ReadString();
        FilePath = filePath;
    }

    public readonly byte Flags;
    public readonly RwmodVersion ModVersion;
    public readonly ushort EntryCount;
    public readonly string Name;
    public readonly string Author;
    public readonly string Homepage;
    public readonly string DisplayName;
    public readonly string FilePath;

    public bool Enabled => State.Instance.Prefs.EnabledMods.Contains(Name);
}
