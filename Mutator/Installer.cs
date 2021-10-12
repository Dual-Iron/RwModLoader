using System.Diagnostics;
using System.Text;
using System.IO.Compression;
using System.Reflection;

namespace Mutator;

public static class Installer
{
    public static async Task SelfUpdate(IEnumerator<string> args)
    {
        GitHubRelease files = await GetRelease("Dual-Iron", "RwModLoader");
        FileVersionInfo myVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? throw new("No process path."));

        // Abort if there's nothing to update
        if (files.Version.ToVersion() <= new Version(myVersion.ProductMajorPart, myVersion.ProductMinorPart, myVersion.ProductBuildPart)) {
            return;
        }

        // Provide the new process with this process's arguments
        StringBuilder processArgs = new();

        while (args.MoveNext()) {
            processArgs.Append($" \"{args.Current}\"");
        }

        // Get a safe temp file name
        string tempFileName = Path.Combine(Path.GetTempPath(), "~rwtemp");

        // Download to that file
        using (Stream download = await files.GetOnlineFileStream(0))
        using (Stream tempFile = File.Create(tempFileName))
            await download.CopyToAsync(tempFile);

        // Run the file
        using Process p = Process.Start(new ProcessStartInfo(tempFileName, processArgs.ToString()) {
            UseShellExecute = false
        }) ?? throw new("No process created.");
    }


    public static void Install()
    {
        InstallSelf();

        if (IsPartialityInstalled()) {
            UninstallPartiality();
        }

        InstallBepInEx();
    }

    private static void InstallSelf()
    {
        string processPath = Environment.ProcessPath ?? throw new("No process path.");
        string copyToDirectory = RwmodsUserFolder.FullName;
        string destFileName = Path.Combine(copyToDirectory, "Mutator.exe");

        if (processPath != destFileName) {
            // Make sure we're not installing an older version
            if (File.Exists(destFileName)) {
                var versionInfo = FileVersionInfo.GetVersionInfo(destFileName);
                var version = new Version(versionInfo.ProductMajorPart, versionInfo.ProductMinorPart, versionInfo.ProductBuildPart, versionInfo.ProductPrivatePart);
                if (version >= typeof(Program).Assembly.GetName().Version) {
                    return;
                }
            }

            File.Copy(processPath, destFileName, true);
        }
    }

    private static bool IsPartialityInstalled()
    {
        if (Directory.Exists(Path.Combine(RwDir, "RainWorld_Data", "Managed_backup"))) {
            return true;
        }

        string modsDir = Path.Combine(RwDir, "Mods");
        if (!Directory.Exists(modsDir)) {
            return false;
        }

        foreach (var item in Directory.GetFiles(modsDir)) {
            if (Path.GetExtension(item) is ".modHash" or ".modMeta") {
                return true;
            }
        }

        return false;
    }

    private static void UninstallPartiality()
    {
        Directory.Delete(Path.Combine(RwDir, "RainWorld_Data", "Managed"), true);
        Directory.Move(Path.Combine(RwDir, "RainWorld_Data", "Managed_backup"), Path.Combine(RwDir, "RainWorld_Data", "Managed"));

        File.Delete(Path.Combine(RwDir, "consoleLog.txt"));
        File.Delete(Path.Combine(RwDir, "exceptionLog.txt"));

        if (Directory.Exists(Path.Combine(RwDir, "PartialityHashes")))
            Directory.Delete(Path.Combine(RwDir, "PartialityHashes"), true);

        if (Directory.Exists(Path.Combine(RwDir, "ModDependencies")))
            Directory.Delete(Path.Combine(RwDir, "ModDependencies"), true);

        if (Directory.Exists(Path.Combine(RwDir, "Mods")))
            foreach (var item in Directory.GetFiles(Path.Combine(RwDir, "Mods"))) {
                if (Path.GetExtension(item) is ".modHash" or ".modMeta") {
                    File.Delete(item);
                }
            }
    }

    public static void Kill(string pid)
    {
        // Give the old process some time to die
        using Process p = Process.GetProcessById(int.Parse(pid));

        // If it's not dead, kill it and wait for it to rot
        try {
            if (!p.HasExited) {
                p.Kill(false);
            }

            p.WaitForExit(5000);
        } catch { }
    }

    public static void Run(string filePath)
    {
        ProcessStartInfo startInfo = new(filePath) {
            CreateNoWindow = false,
            UseShellExecute = false,
        };
        startInfo.EnvironmentVariables.Add("LAUNCHED_FROM_MUTATOR", "true");
        Process.Start(startInfo)?.Dispose();
    }

    public static async Task NeedsSelfUpdate()
    {
        GitHubRelease files = await GetRelease("Dual-Iron", "RwModLoader");
        FileVersionInfo myVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? throw new("No process path."));

        bool needs = files.Version.ToVersion() > new Version(myVersion.ProductMajorPart, myVersion.ProductMinorPart, myVersion.ProductBuildPart);

        Console.WriteLine(needs ? "y" : "n");
    }

    public static void UninstallBepInEx()
    {
        File.Delete(Path.Combine(RwDir, "winhttp.dll"));
    }

    private static void InstallBepInEx()
    {
        static string C(params string[] paths) => Path.Combine(paths);

        string tempDir = "";

        try {
            if (Directory.Exists(C(RwDir, "BepInEx"))) {
                if (Directory.Exists(C(RwDir, "BepInEx", "config"))) {
                    tempDir = Path.GetTempFileName();
                    File.Delete(tempDir);
                    Directory.CreateDirectory(tempDir);
                    Directory.Move(C(RwDir, "BepInEx", "config"), C(tempDir, "config"));
                }

                Directory.Delete(C(RwDir, "BepInEx"), true);
            }

            using Stream rwbep = typeof(Installer).Assembly.GetManifestResourceStream("RwBep") ?? throw new("No stream!");
            using ZipArchive archive = new(rwbep, ZipArchiveMode.Read, true, UseEncoding);
            archive.ExtractToDirectory(RwDir, true);

            if (Directory.Exists(C(tempDir, "config"))) {
                if (Directory.Exists(C(RwDir, "BepInEx", "config")))
                    Directory.Delete(C(C(RwDir, "BepInEx", "config")), true);
                Directory.Move(C(tempDir, "config"), C(RwDir, "BepInEx", "config"));
            }
        } finally {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
