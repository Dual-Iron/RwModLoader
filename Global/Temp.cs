using static System.IO.Path;

namespace Rwml;

readonly struct TempDir : IDisposable
{
    public TempDir()
    {
        string id;

        do {
            id = DateTime.Now.Ticks.ToString("X");
        }
        while (Directory.Exists(Combine(Environment.CurrentDirectory, $"{id}~tmp")));

        Info = Directory.CreateDirectory(Combine(Environment.CurrentDirectory, $"{id}~tmp"));
    }

    public DirectoryInfo Info { get; }
    public string Path => Info.FullName;

    void IDisposable.Dispose()
    {
        Info.Delete(true);
    }
}

readonly struct TempFile : IDisposable
{
    public TempFile()
    {
        string id;

        do {
            id = DateTime.Now.Ticks.ToString("X");
        }
        while (File.Exists(Combine(Environment.CurrentDirectory, $"{id}.tmp")));

        Info = new(Combine(Environment.CurrentDirectory, $"{id}.tmp"));

        Stream = Info.Create();
    }

    public FileStream Stream { get; }
    public FileInfo Info { get; }
    public string Path => Info.FullName;

    void IDisposable.Dispose()
    {
        Stream.Dispose();
        Info.Delete();
    }
}
