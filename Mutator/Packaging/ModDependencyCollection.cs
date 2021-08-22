﻿using System;

namespace Mutator.Packaging
{
    public sealed class ModDependencyCollection
    {
        private readonly string dependencies;

        public ModDependencyCollection(string dependencyString)
        {
            dependencies = dependencyString;
        }

        public ModDependencyCollection(string[] dependencies) : this(string.Join(';', dependencies) + ";")
        { }

        public ModDependencyCollection() : this("")
        { }

        public string[] Dependencies => dependencies.Split(';', StringSplitOptions.RemoveEmptyEntries);

        public override string ToString() => dependencies;
    }
}
