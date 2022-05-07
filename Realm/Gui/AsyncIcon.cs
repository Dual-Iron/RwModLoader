using Realm.Assets;
using Realm.Threading;
using UnityEngine;

namespace Realm.Gui;

public enum AsyncIconStatus { Unstarted, Loading, Loaded, Errored }

sealed class AsyncIcon
{
    private readonly string iconFilename;
    private readonly string iconURL;

    public AsyncIconStatus Status { get; private set; }

    public AsyncIcon(string iconFilename, string iconURL)
    {
        this.iconFilename = iconFilename;
        this.iconURL = iconURL;
    }

    public void Start()
    {
        if (Status == AsyncIconStatus.Unstarted) {
            Status = AsyncIconStatus.Loading;
            NetworkThread.Instance.Enqueue(() => Status = Load());
        }
    }

    private AsyncIconStatus Load()
    {
        if (Futile.atlasManager._allElementsByName.ContainsKey(iconURL)) {
            return AsyncIconStatus.Loaded;
        }

        string path = Path.Combine(RealmPaths.IconFolder.FullName, iconFilename);

        if (File.Exists(path) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalDays < 10) {
            return GetIcon(path);
        }

        var proc = BackendProcess.Execute($"-dl \"{iconURL}\" \"{path}\"");
        if (proc.ExitCode == 0 && File.Exists(path)) {
            return GetIcon(path);
        }

        Program.Logger.LogError($"Error downloading icon for {iconFilename}. {proc}");

        return AsyncIconStatus.Errored;
    }

    private AsyncIconStatus GetIcon(string iconPath)
    {
        using FileStream stream = File.OpenRead(iconPath);

        Texture2D tex = Asset.LoadTexture(stream);

        if (tex.width != 128 || tex.height != 128) {
            Program.Logger.LogError($"Icon texture for {iconFilename} was not 128x128");
            UnityEngine.Object.Destroy(tex);
            return AsyncIconStatus.Errored;
        }
        else {
            HeavyTexturesCache.LoadAndCacheAtlasFromTexture(iconURL, tex);
            return AsyncIconStatus.Loaded;
        }
    }
}
