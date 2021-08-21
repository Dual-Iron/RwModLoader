﻿using System.Reflection;

namespace Realm.ModLoading
{
    public abstract partial class ModDescriptor
    {
        private ModDescriptor() { }

        public abstract bool IsPartiality { get; }

        public abstract void Initialize(Assembly assembly);
        public abstract void Unload();
    }
}
