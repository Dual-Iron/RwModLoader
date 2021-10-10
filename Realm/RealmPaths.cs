namespace Realm;

internal static class RealmPaths
{
    public static DirectoryInfo UserFolder => Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).CreateSubdirectory(".rw");
    public static string ModsFolder => UserFolder.CreateSubdirectory("mods").FullName;
    public static string MutatorPath => Path.Combine(UserFolder.FullName, "Mutator.exe");
}
