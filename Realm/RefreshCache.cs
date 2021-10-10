using Realm.Logging;
using Realm.ModLoading;
using System.Linq;

namespace Realm;

public sealed class RefreshCache
{
    private List<RwmodFileHeader> headers;

    public RefreshCache()
    {
        headers = RwmodFileHeader.GetRwmodHeaders().ToList();
    }

    public void Refresh(IProgressable progressable)
    {
        PluginWrapper.WrapPluginsThenSave(progressable);

        headers = RwmodFileHeader.GetRwmodHeaders().ToList();
    }

    public IEnumerable<RwmodFileHeader> Headers => headers;
}
