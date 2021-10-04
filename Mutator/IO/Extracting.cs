namespace Mutator.IO;

public static class Extracting
{
    public static async Task ExtractAll()
    {
        foreach (var file in Directory.EnumerateFiles(ModsFolder.FullName, "*.rwmod", SearchOption.TopDirectoryOnly)) {
            await DoExtract(file);
        }
    }

    public static async Task Extract(string rwmod)
    {
        string filePath = Path.GetExtension(rwmod) == ".rwmod" && File.Exists(rwmod) ? rwmod : GetModPath(rwmod);

        await DoExtract(filePath);
    }

    private static async Task<Stream> DoExtract(string filePath)
    {
        if (!File.Exists(filePath)) {
            throw ErrFileNotFound(filePath);
        }

        string directory = Path.ChangeExtension(filePath, null);
        string directoryNameSafe = directory;

        int x = 2;
        while (File.Exists(directoryNameSafe) || Directory.Exists(directoryNameSafe)) {
            directoryNameSafe = directory + $" ({x++})";
        }

        Directory.CreateDirectory(directoryNameSafe);

        using Stream rwmodFileStream = File.Open(filePath, FileMode.Open, FileAccess.Read);

        var header = RwmodFileHeader.Read(rwmodFileStream);

        await RwmodOperations.ReadRwmodEntries(
            header,
            rwmodFileStream,
            name => File.Create(Path.Combine(directoryNameSafe, name)));
        return rwmodFileStream;
    }
}
