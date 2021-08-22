using System.IO;

namespace Mutator
{
    public sealed class RaindbMod
    {
        public string? Name;
        public string? Description;
        public string? Author;
        public string? Repo;
        public string? Url;
        public string? IconUrl;
        public string? VideoUrl;
        public string? ModDependencies;

        public override string ToString()
        {
            return Name ?? "Noname";
        }

        public void Write(BinaryWriter writer)
        {
            string?[] order = new[] {
                Name, Description, Author, Repo, Url, IconUrl, VideoUrl, ModDependencies
            };

            foreach (string? item in order) {
                writer.Write(item ?? "");
            }
        }
    }
}
