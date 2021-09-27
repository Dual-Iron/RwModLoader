using System.Collections.ObjectModel;

namespace Realm.ModLoading;

public sealed class RwmodFile
{
    public static RwmodFile[] GetRwmodFiles()
    {
        string[] files = Directory.GetFiles(Extensions.ModsFolder, "*.rwmod", SearchOption.TopDirectoryOnly);

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
        Header = new(Stream);
        Entries = new(FileEntry.GetFileEntries(Header, Stream));
    }

    public readonly string FileName;
    public readonly string FilePath;
    public readonly Stream Stream;
    public readonly RwmodFileHeader Header;
    public readonly ReadOnlyCollection<FileEntry> Entries;
}
