using System.IO;

namespace Mutator.Packaging
{
    // rwmod file format: https://gist.github.com/Dual-Iron/35b71cdd5ffad8b5ad65a3f7214af390
    public sealed class RwmodFileHeader
    {
        public RwmodFileHeader(RwmodFlags flags, RwmodVersion modVersion, string name, string author, string displayName, string homepage)
        {
            Flags = flags;
            ModVersion = modVersion;
            Name = name;
            Author = author;
            DisplayName = displayName;
            Homepage = homepage;
        }

        public static RwmodFileHeader Read(Stream stream)
        {
            using BinaryReader reader = new(stream, InstallerApi.UseEncoding, true);
            return new(flags: (RwmodFlags)reader.ReadInt32(),
                       modVersion: new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte()),
                       name: reader.ReadString(),
                       author: reader.ReadString(),
                       homepage: reader.ReadString(),
                       displayName: reader.ReadString());
        }

        public RwmodFlags Flags { get; set; }
        public RwmodVersion ModVersion { get; set; }
        public string Name { get; }
        public string Author { get; }
        public string DisplayName { get; }
        public string Homepage { get; }

        public bool IsRepo => Homepage.StartsWith("https://github.com/");

        // TODO MEDIUM: define mod dependencies and incompatibilities too

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new(stream, InstallerApi.UseEncoding, true);

            writer.Write((int)Flags);
            writer.Write(ModVersion.Major);
            writer.Write(ModVersion.Minor);
            writer.Write(ModVersion.Patch);
            writer.Write(Name);
            writer.Write(Author);
            writer.Write(Homepage);
            writer.Write(DisplayName);
        }

        public void WriteVersion(Stream stream)
        {
            long pos = stream.Position;
            stream.Position = 1;
            stream.Write(new[] { ModVersion.Major, ModVersion.Minor, ModVersion.Patch });
            stream.Position = pos;
        }

        public void WriteFlags(Stream stream)
        {
            long pos = stream.Position;
            stream.Position = 0;
            stream.Write(new[] { (byte)Flags });
            stream.Position = pos;
        }
    }
}
