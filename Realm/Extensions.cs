using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Realm
{
    internal static class Extensions
    {
#pragma warning disable IDE0029 // Use coalesce expression; Do not use because the == operator must be called.
        private static RainWorld? rw;
        public static RainWorld? RainWorld => rw == null ? rw = UnityEngine.Object.FindObjectOfType<RainWorld>() : rw;
#pragma warning restore IDE0029 // Use coalesce expression

        public static string MutatorPath => Path.Combine(UserFolder, "Mutator.exe");

        // See RwModMutator.InstallerAPI
        public static string UserFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".rw");

        public static string RwDepFolder => Path.Combine(Paths.BepInExRootPath, "rw");

        public static void CopyTo(this Stream from, Stream to)
        {
            byte[] b = new byte[32768];
            int r;
            while ((r = from.Read(b, 0, b.Length)) > 0)
                to.Write(b, 0, r);
        }

        public static IEnumerable<TNode> TopologicalSort<TNode>(this IEnumerable<TNode> nodes, Func<TNode, IEnumerable<TNode>> dependencySelector)
        {
            // https://github.com/BepInEx/BepInEx/blob/82d8daeac165f8454afcd7f94333fe890663ccec/BepInEx/Utility.cs#L129

            List<TNode> sorted_list = new();

            HashSet<TNode> visited = new();
            HashSet<TNode> sorted = new();

            foreach (TNode input in nodes) {
                Stack<TNode> currentStack = new Stack<TNode>();
                if (!Visit(input, currentStack)) {
                    throw new Exception($"Cyclic Dependency:\r\n{currentStack.Select(x => $" - {x}").Aggregate((a, b) => $"{a}\r\n{b}")}");
                }
            }

            return sorted_list;

            bool Visit(TNode node, Stack<TNode> stack)
            {
                if (visited.Contains(node)) {
                    if (!sorted.Contains(node)) {
                        return false;
                    }
                } else {
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
}
