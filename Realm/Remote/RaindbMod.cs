using System.Diagnostics;

namespace Realm.Remote;

public sealed class RaindbMod
{
    public static List<RaindbMod> Fetch()
    {
        string err;
        using MemoryStream ms = new();
        using (Process p = Execution.Begin(Extensions.MutatorPath, "--raindb")) {
            p.StandardOutput.BaseStream.CopyTo(ms);
            err = p.StandardError.ReadToEnd();
            Execution.PolitelyKill(p);
        }

        ms.Position = 0;

        BinaryReader reader = new(ms, Encoding.ASCII);

        List<RaindbMod> mods = new();

        while (reader.PeekChar() != -1) {
            mods.Add(new(reader));
        }

        return mods;
    }

    public readonly bool IsGitHub;
    public readonly string Name;
    public readonly string Description;
    public readonly string Author;
    public readonly string HomepageUrl;
    public readonly string IconUrl;
    public readonly string VideoUrl;

    public RaindbMod(BinaryReader reader)
    {
        Name = reader.ReadString();
        Author = reader.ReadString();
        Description = reader.ReadString();
        HomepageUrl = reader.ReadString();
        IconUrl = reader.ReadString();
        VideoUrl = reader.ReadString();

        IsGitHub = HomepageUrl.StartsWith("https://github.com/");
    }

    public override string ToString()
    {
        return Name ?? "Noname";
    }
}
