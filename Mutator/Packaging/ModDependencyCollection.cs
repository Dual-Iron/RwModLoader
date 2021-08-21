using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Mutator.Packaging
{
    public sealed class ModDependencyCollection : IReadOnlyList<string>
    {
        private readonly string[] dependencies;

        public ModDependencyCollection(string[] dependencies)
        {
            this.dependencies = dependencies;
        }

        public ModDependencyCollection(string dependencyString) : this(dependencyString.Split(';'))
        { }

        public ModDependencyCollection() : this(Array.Empty<string>())
        { }

        public string this[int index] => dependencies[index];

        public int Count => dependencies.Length;

        public IEnumerator<string> GetEnumerator()
        {
            return ((IEnumerable<string>)dependencies).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dependencies.GetEnumerator();
        }

        public override string ToString()
        {
            StringBuilder ret = new();
            foreach (var dependency in dependencies) {
                ret.Append(dependency + ";");
            }
            return ret.ToString();
        }
    }
}
