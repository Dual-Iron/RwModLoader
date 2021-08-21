namespace Mutator
{
    public sealed class RaindbMod
    {
        public string? Name;
        public string? Description;
        public string? Author;
        public string? Url;
        public string? IconUrl;
        public string? VideoUrl;
        public string? ModDependencies;
        public string? Repo;

        public override string ToString()
        {
            return Name ?? "Noname";
        }
    }
}
