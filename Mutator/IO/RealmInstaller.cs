using System.Diagnostics;
using System.IO.Compression;

namespace Mutator.IO;

static class RealmInstaller
{
    public static ExitStatus UserInstall()
    {
        if (ExtIO.RwDir.MatchFailure(out var rwDir, out _)) {
            rwDir = QueryUserForRwDir();
        }

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

            InstallBepInEx(rwDir);
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

    private static void InstallBepInEx(string rwDir)
    {
        static string D(params string[] paths) => Directory.CreateDirectory(Path.Combine(paths)).FullName;

        string tempDir = ExtIO.GetTempDir().FullName;

        using var clearTempDir = new Disposable(() =>
        {
            if (Directory.Exists(tempDir)) {
                Directory.Delete(tempDir, true);
            }
        });

        using (Stream rwbep = typeof(RealmInstaller).Assembly.GetManifestResourceStream("RwBep") ?? throw new("No stream!"))
        using (ZipArchive archive = new(rwbep, ZipArchiveMode.Read, true))
            archive.ExtractToDirectory(tempDir);

        // Move BepInEx/config dir only on fresh installs. This prevents overwriting people's configs.
        var freshInstall = !File.Exists(Path.Combine(rwDir, "BepInEx", "patchers", "Realm.dll"));
        if (freshInstall) {
            CopyDir(D(tempDir, "BepInEx", "config"), D(rwDir, "BepInEx", "config"));
        }

        CopyDir(tempDir, rwDir);
        CopyDir(D(tempDir, "BepInEx", "core"), D(rwDir, "BepInEx", "core"));
        CopyDir(D(tempDir, "BepInEx", "patchers"), D(rwDir, "BepInEx", "patchers"));
    }

    private static void CopyDir(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly)) {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
    }

    private static string QueryUserForRwDir()
    {
        string? rwDir;

        bool helpGiven = false;

        Console.Write("Couldn't find Rain World's installation folder. Please enter it here.\n> ");

        while (true) {
            rwDir = Console.ReadLine();

            Console.WriteLine();

            if (rwDir == null) throw new InvalidOperationException("The console buffer ran out of space!");

            rwDir = ExtIO.CleanRwDir(rwDir);

            if (rwDir != null) return rwDir;

            if (!helpGiven) {
                Console.Write("That path is invalid. Go here for help: [");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("https://savelocation.net/steam-game-folder");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("]\n> ");
                helpGiven = true;
            }
            else {
                Console.Write("Enter Rain World's installation folder.\n> ");
            }
        }
    }
}
