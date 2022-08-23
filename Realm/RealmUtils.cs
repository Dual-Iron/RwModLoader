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

    public static IEnumerable<T> TopologicalSort<T>(this IEnumerable<T> nodes, Func<T, IEnumerable<T>> dependencySelector)
    {
        // https://github.com/BepInEx/BepInEx/blob/82d8daeac165f8454afcd7f94333fe890663ccec/BepInEx/Utility.cs#L129

        List<T> sorted_list = new();

        HashSet<T> visited = new();
        HashSet<T> sorted = new();

        foreach (T input in nodes) {
            Stack<T> currentStack = new();
            if (!Visit(input, currentStack)) {
                throw new Exception($"Cyclic Dependency:\r\n{currentStack.Select(x => $" - {x}").Aggregate((a, b) => $"{a}\r\n{b}")}");
            }
        }

        return sorted_list;

        bool Visit(T node, Stack<T> stack)
        {
            if (visited.Contains(node)) {
                if (!sorted.Contains(node)) {
                    return false;
                }
            }
            else {
                visited.Add(node);

                stack.Push(node);

                foreach (var dep in dependencySelector(node))
                    if (!Visit(dep, stack))
                        return false;


                sorted.Add(node);
                sorted_list.Add(node);

                stack.Pop();
            }

            return true;
        }
    }
}
