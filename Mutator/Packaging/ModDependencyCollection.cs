using System;

namespace Mutator.Packaging
{
    public struct ModDependencyCollection
    {
        private readonly string? dependencies;

        public ModDependencyCollection(string dependencyString)
        {
            dependencies = dependencyString;
        }

        public string[] Dependencies => dependencies == null ? Array.Empty<string>() : dependencies.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        public override string ToString() => dependencies ?? "";
    }
}
