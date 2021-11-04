using System.Collections.ObjectModel;

namespace Realm.ModLoading;

sealed class RwmodFile
{
    public static string[] GetRwmodFilePaths() => Directory.GetFiles(RealmPaths.ModsFolder, "*.rwmod", SearchOption.TopDirectoryOnly);

    public static ICollection<RwmodFile> GetRwmodFiles()
    {
        List<RwmodFile> ret = new();

        string[] files = GetRwmodFilePaths();

        foreach (string filePath in files) {
            if (Read(filePath) is RwmodFile r)
                ret.Add(r);
        }

        return ret;
    }

    public static RwmodFile? Read(string path)
    {
        var fileName = Path.GetFileName(path);
        var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var headerR = RwmodHeader.Read(stream);

        if (headerR.MatchFailure(out var header, out var err)) {
            Program.Logger.LogWarning($"Couldn't read RWMOD file \"{fileName}\": {err}");
            stream.Dispose();
            return null;
        }

        var entries = new ReadOnlyCollection<RwmodFileEntry>(RwmodFileEntry.ReadAll(stream).ToList());

        return new(fileName, path, stream, header, entries);
    }

    public readonly string FileName;
    public readonly string FilePath;
    public readonly Stream Stream;
    public readonly RwmodHeader Header;
    public readonly ReadOnlyCollection<RwmodFileEntry> Entries;

    private RwmodFile(string fileName, string path, FileStream stream, RwmodHeader header, ReadOnlyCollection<RwmodFileEntry> entries)
    {
        FileName = fileName;
        FilePath = path;
        Stream = stream;
        Header = header;
        Entries = entries;
    }
}
