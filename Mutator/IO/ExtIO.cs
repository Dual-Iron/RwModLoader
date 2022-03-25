using Microsoft.Win32;
using System.Text;
using System.Text.RegularExpressions;

namespace Mutator.IO;

static class ExtIO
{
    private static string? rwDir;

    public static string? CleanRwDir(string rwDir)
    {
        if (CleanDir(rwDir) is string dir && File.Exists(Path.Combine(dir, "RainWorld.exe"))) {
            return dir;
        }
        return null;
    }

    public static string? CleanDir(string dir)
    {
        try {
            dir = Path.GetFullPath(dir.Trim());

            if (Directory.Exists(dir)) {
                return dir;
            }
        }
        catch { }

        return null;
    }

    public static Result<string, ExitStatus> RwDir {
        get {
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

    public static Encoding Enc => Encoding.UTF8;

    public static DirectoryInfo UserFolder => Directory.CreateDirectory(Environment.CurrentDirectory);
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

        // Check for parent dirs
        DirectoryInfo? dir = UserFolder;
        while (dir != null) {
            if (CleanRwDir(dir.FullName) is string rwDir) {
                return rwDir;
            }
            dir = dir.Parent;
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

        if (!OperatingSystem.IsWindows()) {
            return ExitStatus.RwFolderNotFound;
        }

        // Find path rigorously
        object? value =
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) ??
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);

        if (value is string steamPath) {
            try {
                string appState = File.ReadAllText(Path.Combine(steamPath, "steamapps", $"appmanifest_{AppID}.acf"));
                var installNameMatch = Regex.Match(appState, @"""installdir""\s*""(.*?)""", RegexOptions.IgnoreCase);
                if (installNameMatch.Success && installNameMatch.Groups.Count == 2) {
                    string installName = installNameMatch.Groups[1].Value;
                    string path = Path.Combine(steamPath, "steamapps", "common", installName);

                    if (CleanRwDir(path) is string rwDir) {
                        return rwDir;
                    }
                }
            }
            catch {
                string fullPath = Path.Combine(steamPath, "steamapps", "common", "Rain World");

                if (CleanRwDir(fullPath) is string rwDir) {
                    return rwDir;
                }
            }
        }

        return ExitStatus.RwFolderNotFound;
    }
}
