using System.Diagnostics;
using System.Text;
#if !INSTALL_ONLY
using System.IO.Compression;
using System.Reflection;
#endif

namespace Mutator;

public static class Installer
{
    public static async Task SelfUpdate(IEnumerator<string> args)
    {
        await VerifyInternetConnection();

        GitHubRelease files = await GetRelease("Dual-Iron", "RwModLoader");
        FileVersionInfo myVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? throw new("No process path."));

#if !INSTALL_ONLY
        // Abort if there's nothing to update
        if (files.Version.ToVersion() <= new Version(myVersion.ProductMajorPart, myVersion.ProductMinorPart, myVersion.ProductBuildPart)) {
            return;
        }
#endif

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


#if !INSTALL_ONLY
    public static void Install()
    {
        InstallSelf();

        if (IsPartialityInstalled()) {
            UninstallPartiality();
        }

        // TODO HIGH: allow mutator to overwrite Realm.dll even if BepInEx *is* installed
        // preferably without deleting the user's config
        if (!IsBepInExInstalled()) {
            InstallBepInEx();
        }
    }

    private static void InstallSelf()
    {
        string processPath = Environment.ProcessPath ?? throw new("No process path.");
        string copyToDirectory = RwmodsUserFolder.FullName;
        string destFileName = Path.Combine(copyToDirectory, "Mutator.exe");

        if (processPath != destFileName)
            File.Copy(processPath, destFileName, true);
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
            var ext = Path.GetExtension(item);
            if (ext == ".modHash" || ext == ".modMeta") {
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

        foreach (var item in Directory.GetFiles(Path.Combine(RwDir, "Mods"))) {
            var ext = Path.GetExtension(item);
            if (ext == ".modHash" || ext == ".modMeta") {
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
        await VerifyInternetConnection();

        GitHubRelease files = await GetRelease("Dual-Iron", "RwModLoader");
        FileVersionInfo myVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? throw new("No process path."));

        bool needs = files.Version.ToVersion() > new Version(myVersion.ProductMajorPart, myVersion.ProductMinorPart, myVersion.ProductBuildPart);

        Console.WriteLine(needs ? "y" : "n");
    }

    public static void UninstallBepInEx()
    {
        if (!IsBepInExInstalled()) {
            return;
        }

        Directory.Delete(Path.Combine(RwDir, "BepInEx"), true);
        File.Delete(Path.Combine(RwDir, "winhttp.dll"));
        File.Delete(Path.Combine(RwDir, "doorstop_config.ini"));
    }

    private static bool IsBepInExInstalled()
    {
        string bepInExCoreDirectory = Path.Combine(RwDir, "BepInEx", "core");
        if (!Directory.Exists(bepInExCoreDirectory)) {
            return false;
        }

        string patcherFilePath = Path.Combine(RwDir, "BepInEx", "patchers", "Realm.dll");
        if (!File.Exists(patcherFilePath)) {
            return false;
        }

        try {
            var bepInEx = AssemblyName.GetAssemblyName(Path.Combine(bepInExCoreDirectory, "BepInEx.dll"));
            return bepInEx.Name == "BepInEx" && bepInEx.Version >= new Version(5, 4, 15);
        } catch (BadImageFormatException) {
            return false;
        } catch (FileNotFoundException) {
            return false;
        }
    }

    private static void InstallBepInEx()
    {
        UninstallBepInEx();

        try {
            using Stream rwbep = typeof(Installer).Assembly.GetManifestResourceStream("RwBep") ?? throw new("No stream!");
            using ZipArchive archive = new(rwbep, ZipArchiveMode.Read, true, UseEncoding);
            archive.ExtractToDirectory(RwDir, true);
        } catch (AggregateException e) {
            throw e.Flatten();
        }
    }
#endif
}
