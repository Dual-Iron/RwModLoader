using Realm.Logging;

namespace Realm.ModLoading;

sealed class RefreshCache
{
    // This collection should never be accessed while `Refresh`ing for thread-safety.
    private ICollection<RwmodFileHeader> headers = RwmodFileHeader.GetRwmodHeaders();

    public void Refresh(Progressable progressable)
    {
        State.Prefs.Load();

        PluginWrapper.WrapPlugins(progressable, out _);

        if (progressable.Errors) return;

        headers = RwmodFileHeader.GetRwmodHeaders();
    }

    public IEnumerable<RwmodFileHeader> Headers => headers;
}
