namespace Realm;

internal static class RealmPaths
{
    public static DirectoryInfo UserFolder => new FileInfo(Path.GetFullPath(BepInEx.Preloader.EnvVars.DOORSTOP_INVOKE_DLL_PATH)).Directory.Parent.CreateSubdirectory("realm");
    public static DirectoryInfo ModsFolder => UserFolder.CreateSubdirectory("mods");
    public static DirectoryInfo IconFolder => UserFolder.CreateSubdirectory("icons");
    public static string BackendPath => Path.Combine(UserFolder.FullName, "backend.exe");
}
