using System;
using System.IO;

namespace Mutator.Packaging
{
    // rwmod file format: https://gist.github.com/Dual-Iron/35b71cdd5ffad8b5ad65a3f7214af390
    public sealed class RwmodFileHeader
    {
        [Flags]
        public enum RwmodFlags : int
        {
            IsUnwrapped = 1
        }

        public RwmodFileHeader(RwmodFlags flags, RwmodVersion modVersion, string repositoryName, string author, string displayName, ModDependencyCollection modDependencies)
        {
            Flags = flags;
            ModVersion = modVersion;
            RepositoryName = repositoryName;
            Author = author;
            DisplayName = displayName;
            ModDependencies = modDependencies;
        }

        public static RwmodFileHeader Read(Stream stream)
        {
            using BinaryReader reader = new(stream, InstallerApi.UseEncoding, true);
            return new(flags: (RwmodFlags)reader.ReadInt32(),
                       modVersion: new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte()),
                       repositoryName: reader.ReadString(),
                       author: reader.ReadString(),
                       displayName: reader.ReadString(),
                       modDependencies: new(reader.ReadString()));
        }

        public RwmodFlags Flags { get; set; }
        public RwmodVersion ModVersion { get; set; }
        public string RepositoryName { get; }
        public string Author { get; }
        public string DisplayName { get; }
        public ModDependencyCollection ModDependencies { get; }

        // TODO MEDIUM: more well-defined dependencies. e.g. write 3 version bytes then the mod's name.
        // TODO MEDIUM: store mod names inside the file, separate from filename

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new(stream, InstallerApi.UseEncoding, true);

            writer.Write((int)Flags);
            writer.Write(ModVersion.Major);
            writer.Write(ModVersion.Minor);
            writer.Write(ModVersion.Patch);
            writer.Write(RepositoryName);
            writer.Write(Author);
            writer.Write(DisplayName);
            writer.Write(ModDependencies.ToString());
        }

        public void WriteVersion(Stream stream)
        {
            long pos = stream.Position;
            stream.Position = 4;
            stream.Write(new[] { ModVersion.Major, ModVersion.Minor, ModVersion.Patch });
            stream.Position = pos;
        }

        public void WriteFlags(Stream stream)
        {
            int flags = (int)Flags;
            long pos = stream.Position;
            stream.Position = 0;
            stream.Write(new[] { (byte)flags, (byte)(flags >> 8), (byte)(flags >> 16), (byte)(flags >> 24) });
            stream.Position = pos;
        }
    }
}
