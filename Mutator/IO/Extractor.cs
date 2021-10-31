namespace Mutator.IO;

static class Extractor
{
    public static ExitStatus Extract(string filePath)
    {
        if (Path.GetExtension(filePath) == ".rwmod" && File.Exists(filePath))
            return DoExtract(filePath);
        else
            return DoExtract(ExtIO.GetModPath(filePath));
    }

    private static ExitStatus DoExtract(string filePath)
    {
        if (!File.Exists(filePath)) {
            return ExitStatus.FileNotFound(filePath);
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

        RwmodOperations.ReadRwmodEntries(
            header,
            rwmodFileStream,
            name => File.Create(Path.Combine(directoryNameSafe, name))
            );

        return ExitStatus.Success;
    }
}
