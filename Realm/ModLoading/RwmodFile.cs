using System.IO;

namespace Realm.ModLoading
{
    public sealed class RwmodFile
    {
        // rwmod file format: https://gist.github.com/Dual-Iron/35b71cdd5ffad8b5ad65a3f7214af390
        public RwmodFile(string path, string name, BinaryReader reader)
        {
            Flags = reader.ReadInt32();
            ModVersion = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            RepositoryName = reader.ReadString();
            RepositoryAuthor = reader.ReadString();
            DisplayName = reader.ReadString();
            ModDependencies = new(reader.ReadString());
            Path = path;
            Name = name;
        }

        // Null if created and not read from
        public int Flags { get; }
        public RwmodVersion ModVersion { get; }
        public string RepositoryName { get; }
        public string RepositoryAuthor { get; }
        public string DisplayName { get; }
        public ModDependencyCollection ModDependencies { get; }
        public string Path { get; }
        public string Name { get; }
    }
}