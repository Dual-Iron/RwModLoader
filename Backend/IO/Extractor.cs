namespace Backend.IO;

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

        using Stream rwmodStream = File.Open(filePath, FileMode.Open, FileAccess.Read);

        if (RwmodHeader.Read(rwmodStream).MatchFailure(out _, out var err)) {
            return ExitStatus.CorruptRwmod(filePath, err);
        }

        foreach (var entry in RwmodFileEntry.ReadAll(rwmodStream)) {
            using Stream fs = File.Create(Path.Combine(directoryNameSafe, entry.Name));

            RwmodIO.CopyStream(new SpliceStream(rwmodStream, entry.Offset, entry.Size), fs, entry.Size);
        }

        return ExitStatus.Success;
    }
}
