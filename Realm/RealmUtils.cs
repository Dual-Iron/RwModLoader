namespace Realm;

internal static class RealmUtils
{
#pragma warning disable IDE0029 // Do not use ??= because the == operator must be called.
    private static RainWorld? rw;
    public static RainWorld? RainWorld => rw == null ? rw = UnityEngine.Object.FindObjectOfType<RainWorld>() : rw;
#pragma warning restore IDE0029

    public static bool Enabled(this RwmodHeader header) => State.Prefs.EnabledMods.Contains(header.Name);

    public static void CopyTo(this Stream from, Stream to)
    {
        byte[] b = new byte[32768];
        int r;
        while ((r = from.Read(b, 0, b.Length)) > 0)
            to.Write(b, 0, r);
    }

    public static IEnumerable<T> LooseTopologicalSort<T>(this IEnumerable<T> nodes, Func<T, IEnumerable<T>> dependencySelector)
    {
        List<T> sorted = new();
        HashSet<T> visited = new();

        foreach (T input in nodes) {
            Visit(input);
        }

        return sorted;

        void Visit(T node)
        {
            if (visited.Add(node)) {
                foreach (var dep in dependencySelector(node)) {
                    Visit(node);
                }
                sorted.Add(node);
            }
        }
    }
}
