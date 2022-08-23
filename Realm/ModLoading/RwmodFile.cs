using Rwml;
using System.Collections.ObjectModel;

namespace Realm.ModLoading;

sealed class RwmodFile
{
    public static string[] GetRwmodFilePaths() => Directory.GetFiles(RealmPaths.ModsFolder.FullName, "*.rwmod", SearchOption.TopDirectoryOnly);

    public static Result<List<RwmodFile>, string> GetRwmodFiles()
    {
        List<RwmodFile> ret = new();

        string[] files = GetRwmodFilePaths();

        foreach (string filePath in files) {
            if (Read(filePath).MatchSuccess(out var r, out var err))
                ret.Add(r);
            else
                return $"There was an error while reading {Path.GetFileName(filePath)}: {err}";
        }

        return ret;
    }

    public static Result<RwmodFile, string> Read(string path)
    {
        var fileName = Path.GetFileName(path);
        var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var headerR = RwmodHeader.Read(stream);

        if (headerR.MatchFailure(out var header, out var err)) {
            stream.Dispose();
            return err;
        }

        var entries = new ReadOnlyCollection<RwmodFileEntry>(RwmodFileEntry.ReadAll(stream).ToList());

        return new RwmodFile(fileName, path, stream, header, entries);
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

    public override string ToString()
    {
        return Header.ToString();
    }
}
