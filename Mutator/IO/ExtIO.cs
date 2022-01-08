using Microsoft.Win32;
using System.Text;
using System.Text.RegularExpressions;

namespace Mutator.IO;

static class ExtIO
{
    private static string? rwDir;

    public static string? CleanRwDir(string rwDir)
    {
        try {
            rwDir = Path.GetFullPath(rwDir.Trim());

            if (Directory.Exists(rwDir) && File.Exists(Path.Combine(rwDir, "RainWorld.exe"))) {
                return rwDir;
            }
        }
        catch { }

        return null;
    }

    public static Result<string, ExitStatus> RwDir
    {
        get
        {
            if (rwDir != null) return rwDir;

            try {
                var result = GetRwDir();
                if (result.MatchSuccess(out var dir, out _)) {
                    rwDir = dir;
                }
                return result;
            }
            catch (IOException) {
                return ExitStatus.RwFolderNotFound;
            }
        }
    }

    public static Encoding Enc => Encoding.Unicode;

    private readonly static string userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".rw");

    public static DirectoryInfo UserFolder => Directory.CreateDirectory(userPath);
    public static DirectoryInfo ModsFolder => UserFolder.CreateSubdirectory("mods");
    public static DirectoryInfo BackupsFolder => UserFolder.CreateSubdirectory("backups");

    public static string GetModPath(string name) => Path.Combine(ModsFolder.FullName, Path.ChangeExtension(name, ".rwmod"));

    public static DirectoryInfo GetTempDir()
    {
        string temp = Path.GetTempFileName();
        File.Delete(temp);
        return Directory.CreateDirectory(temp);
    }

    private static Result<string, ExitStatus> GetRwDir()
    {
        const int AppID = 312520;

        // Check for explicit path override
        if (File.Exists("path.txt")) {
            if (File.ReadLines("path.txt").FirstOrDefault() is string firstLine && CleanRwDir(firstLine) is string rwDir) {
                return rwDir;
            }
            return ExitStatus.RwPathInvalid;
        }

        // Check simple, common paths
        string[] commonPaths = {
            @"C:\Program Files (x86)\Steam\steamapps\common\Rain World",
            @"C:\Program Files\Steam\steamapps\common\Rain World"
        };

        foreach (var path in commonPaths) {
            if (CleanRwDir(path) is string rwDir) {
                return rwDir;
            }
        }

        if (!OperatingSystem.IsWindows()) return ExitStatus.RwFolderNotFound;

        // Find path rigorously
        object? value =
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) ??
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);

        if (value is string steamPath) {
            string appState = File.ReadAllText(Path.Combine(steamPath, "steamapps", $"appmanifest_{AppID}.acf"));
            var installNameMatch = Regex.Match(appState, @"""installdir""\s*""(.*?)""", RegexOptions.IgnoreCase);
            if (installNameMatch.Success && installNameMatch.Groups.Count == 2) {
                string installName = installNameMatch.Groups[1].Value;
                string path = Path.Combine(steamPath, "steamapps", "common", installName);

                if (CleanRwDir(path) is string rwDir) {
                    File.WriteAllText("path.txt", rwDir + "\n");
                    return rwDir;
                }
            }
        }

        return ExitStatus.RwFolderNotFound;
    }
}
