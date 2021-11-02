using Realm.Logging;

namespace Realm.ModLoading;

sealed class RefreshCache
{
    private List<RwmodFileHeader> headers;

    public RefreshCache()
    {
        headers = RwmodFileHeader.GetRwmodHeaders().ToList();
    }

    public void Refresh(IProgressable progressable)
    {
        State.Instance.Prefs.Load();

        PluginWrapper.WrapPlugins(progressable, out _);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        headers = RwmodFileHeader.GetRwmodHeaders().ToList();
    }

    public IEnumerable<RwmodFileHeader> Headers => headers;
}
