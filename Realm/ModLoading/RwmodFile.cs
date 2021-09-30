using System.Collections.ObjectModel;

namespace Realm.ModLoading;

public sealed class RwmodFile
{
    public static string[] GetRwmodFilePaths() => Directory.GetFiles(Extensions.ModsFolder, "*.rwmod", SearchOption.TopDirectoryOnly);

    public static RwmodFile[] GetRwmodFiles()
    {
        string[] files = GetRwmodFilePaths();

        RwmodFile[] ret = new RwmodFile[files.Length];

        for (int i = 0; i < ret.Length; i++) {
            ret[i] = new(files[i]);
        }

        return ret;
    }

    public RwmodFile(string path)
    {
        FileName = Path.GetFileName(path);
        FilePath = path;
        Stream = File.Open(path, FileMode.Open, FileAccess.Read);
        Header = new(path, Stream);
        Entries = new(FileEntry.GetFileEntries(Header, Stream));
    }

    public readonly string FileName;
    public readonly string FilePath;
    public readonly Stream Stream;
    public readonly RwmodFileHeader Header;
    public readonly ReadOnlyCollection<FileEntry> Entries;
}
