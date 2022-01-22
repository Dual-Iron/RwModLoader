namespace Realm;

internal static class RealmPaths
{
    public static DirectoryInfo UserFolder => new FileInfo(Path.GetFullPath(BepInEx.Preloader.EnvVars.DOORSTOP_INVOKE_DLL_PATH)).Directory.Parent.CreateSubdirectory("realm");
    public static string ModsFolder => UserFolder.CreateSubdirectory("mods").FullName;
    public static string MutatorPath => Path.Combine(UserFolder.FullName, "backend.exe");
}
