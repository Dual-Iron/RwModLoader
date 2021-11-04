using Realm.Logging;

namespace Realm.ModLoading;

sealed class RefreshCache
{
    private ICollection<RwmodFileHeader> headers;

    public RefreshCache()
    {
        headers = RwmodFileHeader.GetRwmodHeaders();
    }

    public void Refresh(IProgressable progressable)
    {
        State.Prefs.Load();

        PluginWrapper.WrapPlugins(progressable, out _);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        headers = RwmodFileHeader.GetRwmodHeaders();
    }

    public IEnumerable<RwmodFileHeader> Headers => headers;
}
