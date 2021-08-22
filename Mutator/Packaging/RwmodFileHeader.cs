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

        public RwmodFileHeader(RwmodFlags flags, RwmodVersion modVersion, string repositoryName, string repositoryAuthor, string displayName, ModDependencyCollection modDependencies)
        {
            Flags = flags;
            ModVersion = modVersion;
            RepositoryName = repositoryName;
            RepositoryAuthor = repositoryAuthor;
            DisplayName = displayName;
            ModDependencies = modDependencies;
        }

        public static RwmodFileHeader ReadFrom(Stream stream)
        {
            using BinaryReader reader = new(stream, InstallerApi.UseEncoding, true);
            return new(flags: (RwmodFlags)reader.ReadInt32(),
                       modVersion: new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte()),
                       repositoryName: reader.ReadString(),
                       repositoryAuthor: reader.ReadString(),
                       displayName: reader.ReadString(),
                       modDependencies: new(reader.ReadString()));
        }

        // Null if created and not read from
        public RwmodFlags Flags { get; set; }
        public RwmodVersion ModVersion { get; set; }
        public string RepositoryName { get; }
        public string RepositoryAuthor { get; }
        public string DisplayName { get; }
        public ModDependencyCollection ModDependencies { get; }

        public void WriteTo(Stream stream)
        {
            using BinaryWriter writer = new(stream, InstallerApi.UseEncoding, true);

            writer.Write((int)Flags);
            writer.Write(ModVersion.Major);
            writer.Write(ModVersion.Minor);
            writer.Write(ModVersion.Patch);
            writer.Write(RepositoryName);
            writer.Write(RepositoryAuthor);
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
            long pos = stream.Position;
            stream.Position = 0;
            stream.Write(BitConverter.GetBytes((int)Flags));
            stream.Position = pos;
        }
    }
}
