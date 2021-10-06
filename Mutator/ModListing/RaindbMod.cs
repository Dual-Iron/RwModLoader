namespace Mutator.ModListing;

public sealed class RaindbMod
{
    public string? Name;
    public string? Description;
    public string? Author;
    public string? Url;
    public string? IconUrl;
    public string? VideoUrl;

    public override string ToString()
    {
        return Name ?? "Noname";
    }

    public void Write(BinaryWriter writer)
    {
        foreach (string? item in new[] { Name, Author, Description, Url, IconUrl, VideoUrl }) {
            writer.Write(item ?? "");
        }
    }

    public static async Task PrintAll()
    {
        Stream stdout = Console.OpenStandardOutput();

        using BinaryWriter writer = new(stdout, UseEncoding, true);

        foreach (var mod in await ModList.GetMods()) {
            mod.Write(writer);
        }
    }
}
