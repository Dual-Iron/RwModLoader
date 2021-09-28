using Realm.ModLoading;
using System.Linq;

namespace Realm;

public sealed class RwmodHeaderCache
{
    private List<RwmodFileHeader> headers;

    public RwmodHeaderCache()
    {
        headers = RwmodFileHeader.GetRwmodHeaders().ToList();
    }

    public void Refresh()
    {
        headers = RwmodFileHeader.GetRwmodHeaders().ToList();
    }

    public IEnumerable<RwmodFileHeader> Headers => headers;
}
