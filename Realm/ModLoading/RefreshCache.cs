﻿using Realm.Logging;

namespace Realm.ModLoading;

sealed class RefreshCache
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