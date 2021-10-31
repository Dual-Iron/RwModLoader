using Microsoft.Win32;
using System.Text;
using System.Text.RegularExpressions;

namespace Mutator.IO;

static class ExtIO
{
    private static string? rwDir;

    public static Result<string, ExitStatus> RwDir {
        get {
            if (rwDir != null) return rwDir;

            var result = GetRwDir();
            result.MatchSuccess(out rwDir, out _);
            return result;
        }
    }

    public static Encoding Enc => Encoding.ASCII;

    public static string UserPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".rw");

    public static DirectoryInfo UserFolder => Directory.CreateDirectory(UserPath);
    public static DirectoryInfo ModsFolder => UserFolder.CreateSubdirectory("mods");
    public static DirectoryInfo BackupsFolder => UserFolder.CreateSubdirectory("patch-backups");

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
            if (File.ReadLines("path.txt").FirstOrDefault() is string firstLine) {
                var path = Path.GetFullPath(firstLine.Trim());
                if (path.Length > 0 && Directory.Exists(path)) {
                    return path;
                }
                return ExitStatus.FolderNotFound(path);
            }
            return ExitStatus.RwPathInvalid;
        }

        // Check simple, common paths
        string[] commonPaths = new[] {
            @"C:\Program Files (x86)\Steam\steamapps\common\Rain World",
            @"C:\Program Files\Steam\steamapps\common\Rain World"
        };

        foreach (var path in commonPaths) {
            if (Directory.Exists(path)) {
                return path;
            }
        }

        // Find path rigorously
        if (OperatingSystem.IsWindows()) {
            object? value =
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) ??
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);

            if (value is string steamPath) {
                string appState = File.ReadAllText(Path.Combine(steamPath, "steamapps", $"appmanifest_{AppID}.acf"));
                var installNameMatch = Regex.Match(appState, @"""installdir""\s*""(.*?)""");
                if (installNameMatch.Success && installNameMatch.Groups.Count == 2) {
                    string installName = installNameMatch.Groups[1].Value;
                    string path = Path.Combine(steamPath, "steamapps", "common", installName);

                    if (Directory.Exists(path)) {
                        File.WriteAllText("path.txt", path + "\n");
                        return path;
                    }
                }
            }
        }

        return ExitStatus.RwFolderNotFound;
    }
}
