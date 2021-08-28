using System.IO;
using System.Text;

namespace Realm.ModLoading
{
    public sealed class RwmodFile
    {
        public static RwmodFile[] FetchAll()
        {
            string[] files = Directory.GetFiles(Path.Combine(Extensions.UserFolder, "mods"), "*.rwmod", SearchOption.TopDirectoryOnly);

            RwmodFile[] rwmodFiles = new RwmodFile[files.Length];

            for (int i = 0; i < files.Length; i++) {
                rwmodFiles[i] = new(files[i], Path.GetFileNameWithoutExtension(files[i]));
            }

            return rwmodFiles;
        }

        // rwmod file format: https://gist.github.com/Dual-Iron/35b71cdd5ffad8b5ad65a3f7214af390
        public RwmodFile(string path, string name)
        {
            FilePath = path;
            Name = name;

            using BinaryReader reader = new(File.OpenRead(FilePath), Encoding.ASCII);

            Flags = reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadByte() << 16) | (reader.ReadByte() << 24);
            ModVersion = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            RepositoryName = reader.ReadString();
            Author = reader.ReadString();
            DisplayName = reader.ReadString();
            ModDependencies = new(reader.ReadString());
        }

        public int Flags { get; }
        public RwmodVersion ModVersion { get; }
        public string RepositoryName { get; }
        public string Author { get; }
        public string DisplayName { get; }
        public ModDependencyCollection ModDependencies { get; }
        public string FilePath { get; }
        public string Name { get; }

        public bool Enabled => ProgramState.Current.Prefs.EnabledMods.Contains(Name);
    }
}