using System.IO;

namespace Mutator.Packaging
{
    // rwmod file format: https://gist.github.com/Dual-Iron/35b71cdd5ffad8b5ad65a3f7214af390
    public sealed class RwmodFileHeader
    {
        public const int Position_Version = 0;

        private RwmodFileHeader(RwmodVersion modVersion, string repositoryName, string repositoryAuthor, string displayName, ModDependencyCollection modDependencies)
        {
            ModVersion = modVersion;
            RepositoryName = repositoryName;
            RepositoryAuthor = repositoryAuthor;
            DisplayName = displayName;
            ModDependencies = modDependencies;
        }

        // Null if created and not read from
        public RwmodVersion ModVersion { get; }
        public string RepositoryName { get; }
        public string RepositoryAuthor { get; }
        public string DisplayName { get; }
        public ModDependencyCollection ModDependencies { get; }

        public void WriteTo(Stream stream)
        {
            using BinaryWriter writer = new(stream, InstallerApi.UseEncoding, true);

            writer.Write(ModVersion.Major);
            writer.Write(ModVersion.Minor);
            writer.Write(ModVersion.Patch);
            writer.Write(RepositoryName);
            writer.Write(RepositoryAuthor);
            writer.Write(DisplayName);
            writer.Write(ModDependencies.ToString());
        }

        public static RwmodFileHeader Create(RwmodVersion modVersion, string repositoryName, string repositoryAuthor, string displayName, ModDependencyCollection modDependencies)
        {
            return new(modVersion, repositoryName, repositoryAuthor, displayName, modDependencies);
        }

        public static RwmodFileHeader ReadFrom(Stream stream)
        {
            using BinaryReader reader = new(stream, InstallerApi.UseEncoding, true);
            return new(
                new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte()),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                new(reader.ReadString())
                );
        }
    }
}
