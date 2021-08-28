using System;

namespace Realm.ModLoading
{
    public struct ModDependencyCollection
    {
        private readonly string? dependencies;

        public ModDependencyCollection(string dependencyString)
        {
            dependencies = dependencyString;
        }

        public string[] Dependencies => dependencies == null ? new string[0] : dependencies.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        public override string ToString() => dependencies ?? "";
    }
}