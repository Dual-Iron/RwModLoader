using System.Diagnostics;
using System.IO.Compression;

namespace Backend.IO;

static class RealmInstaller
{
    public static ExitStatus UserInstall()
    {
        string rwDir = ExtIO.RwDir.MatchSuccess(out var s, out _) ? QueryUserForRwDir(s) : QueryUserForRwDir(null);

        var procs = Process.GetProcessesByName("RainWorld");
        if (procs.Length > 0) {
            Console.WriteLine("Waiting for Rain World to close...");
            foreach (var proc in procs) {
                proc.Kill(true);

                if (!proc.WaitForExit(3000)) {
                    return ExitStatus.IOError("Close Rain World before continuing.");
                }

                proc.Dispose();
            }
        }

        Console.WriteLine("Installing Realm...");

        try {
            DoInstall(rwDir);
        }
        catch (Exception e) {
            return ExitStatus.IOError(e.Message);
        }

        Console.Write("Installed Realm. Press ENTER to start Rain World, or press any other key to exit. ");

        if (Console.ReadKey(true).Key == ConsoleKey.Enter) {
            Process.Start(new ProcessStartInfo {
                FileName = "steam://run/312520",
                UseShellExecute = true,
                Verb = "open"
            })?.Dispose();
        }

        return ExitStatus.Success;
    }

    public static ExitStatus Install()
    {
        if (ExtIO.RwDir.MatchFailure(out var rwDir, out var err)) {
            return err;
        }

        try {
            DoInstall(rwDir);
        }
        catch (Exception e) {
            return ExitStatus.IOError(e.Message);
        }

        return ExitStatus.Success;
    }

    private static void DoInstall(string rwDir)
    {
        if (InstallSelf(rwDir)) {
            if (IsPartialityInstalled(rwDir)) {
                UninstallPartiality(rwDir);
            }

            InstallRwBep(rwDir);
        }
    }

    private static bool InstallSelf(string rwDir)
    {
        string processPath = Environment.ProcessPath ?? throw new("No process path.");
        string destFileName = Path.Combine(rwDir, "BepInEx", "realm", "backend.exe");

        if (processPath != destFileName) {
            // Make sure we're not installing an older version
            if (File.Exists(destFileName)) {
                var versionInfo = FileVersionInfo.GetVersionInfo(destFileName);
                var version = new Version(versionInfo.ProductMajorPart, versionInfo.ProductMinorPart, versionInfo.ProductBuildPart, versionInfo.ProductPrivatePart);
                if (version > typeof(Program).Assembly.GetName().Version) {
                    return false;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destFileName)!);
            File.Copy(processPath, destFileName, true);
        }
        return true;
    }

    private static bool IsPartialityInstalled(string rwDir)
    {
        return Directory.Exists(Path.Combine(rwDir, "Mods"))
            && Directory.Exists(Path.Combine(rwDir, "RainWorld_Data", "Managed_backup"))
            && Directory.EnumerateFiles(Path.Combine(rwDir, "RainWorld_Data", "Managed_backup")).Any();
    }

    private static void UninstallPartiality(string rwDir)
    {
        Directory.Delete(Path.Combine(rwDir, "RainWorld_Data", "Managed"), true);
        CopyDir(Path.Combine(rwDir, "RainWorld_Data", "Managed_backup"), Path.Combine(rwDir, "RainWorld_Data", "Managed"));

        File.Delete(Path.Combine(rwDir, "consoleLog.txt"));
        File.Delete(Path.Combine(rwDir, "exceptionLog.txt"));

        if (Directory.Exists(Path.Combine(rwDir, "PartialityHashes")))
            Directory.Delete(Path.Combine(rwDir, "PartialityHashes"), true);

        if (Directory.Exists(Path.Combine(rwDir, "ModDependencies")))
            Directory.Delete(Path.Combine(rwDir, "ModDependencies"), true);

        if (Directory.Exists(Path.Combine(rwDir, "Mods")))
            foreach (var item in Directory.GetFiles(Path.Combine(rwDir, "Mods"))) {
                if (Path.GetExtension(item) is ".modHash" or ".modMeta") {
                    File.Delete(item);
                }
            }
    }

    static void Try(Action a)
    {
        try { a(); } catch { }
    }

    private static void InstallRwBep(string rwDir)
    {
        // Delete old installation files
        if (Directory.Exists(Path.Combine(rwDir, "BepInEx", "patchers")))
            Try(() => Directory.Delete(Path.Combine(rwDir, "BepInEx", "patchers"), true));

        if (Directory.Exists(Path.Combine(rwDir, "BepInEx", "plugins")))
            Try(() => Directory.Delete(Path.Combine(rwDir, "BepInEx", "plugins"), true));

        if (Directory.Exists(Path.Combine(rwDir, "BepInEx", "core")))
            Try(() => Directory.Delete(Path.Combine(rwDir, "BepInEx", "core"), true));

        // Copy existing ones
        static string D(params string[] paths) => Directory.CreateDirectory(Path.Combine(paths)).FullName;

        using TempDir temp = new();

        bool freshInstall = !File.Exists(Path.Combine(rwDir, "BepInEx", "patchers", "Realm.dll"));

        using (Stream rwbep = typeof(RealmInstaller).Assembly.GetManifestResourceStream("RwBep") ?? throw new("No stream!"))
        using (ZipArchive archive = new(rwbep, ZipArchiveMode.Read, true))
            archive.ExtractToDirectory(temp.Path);

        CopyDir(temp.Path, rwDir);
        CopyDir(D(temp.Path, "BepInEx", "core"), D(rwDir, "BepInEx", "core"));
        CopyDir(D(temp.Path, "BepInEx", "patchers"), D(rwDir, "BepInEx", "patchers"));

        if (freshInstall) {
            CopyDir(D(temp.Path, "BepInEx", "config"), D(rwDir, "BepInEx", "config"));
        }
    }

    private static void CopyDir(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly)) {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
    }

    private static string QueryUserForRwDir(string? rwDir)
    {
        bool helpGiven = false;

        if (rwDir != null) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(rwDir);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("Found Rain World's Steam installation folder. Should Realm install there? (y/n)\n> ");

            while (true) {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Y) {
                    Console.Write("y\n\n");
                    return rwDir;
                }
                if (key.Key == ConsoleKey.N) {
                    Console.Write("n\n\n");
                    break;
                }
            }
        }

        Console.Write("Where should Realm install?\n> ");

        while (true) {
            string dir = Console.ReadLine() ?? throw new InvalidOperationException("The console buffer ran out of space!");

            if (ExtIO.CleanDir(dir) is string cleanDir) {
                Console.WriteLine();
                return cleanDir;
            }

            if (!helpGiven) {
                Console.Write("\nThat path is not a valid directory. Go here for help: [");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("https://savelocation.net/steam-game-folder");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("]\n> ");
                helpGiven = true;
            }
            else {
                Console.Write("> ");
            }
        }
    }
}
