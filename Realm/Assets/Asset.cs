using System.Collections;
using UnityEngine;

namespace Realm.Assets;

// @henpemaz
// https://gist.github.com/Dual-Iron/2481ac0528f3eedfa827ab7cfbef02ec

static class Asset
{
    /// <summary>
    /// Gets a sprite from this assembly's embedded resources.
    /// </summary>
    public static FSprite SpriteFromRes(string name)
    {
        return LoadSprite(name, () => typeof(Program).Assembly.GetManifestResourceStream(name) ?? throw new ArgumentNullException());
    }

    /// <summary>
    /// Gets a sprite from a stream.
    /// </summary>
    public static FSprite LoadSprite(string name, Func<Stream> getStream)
    {
        if (Futile.atlasManager._allElementsByName.TryGetValue(name, out var elem)) {
            return new(elem);
        }
        Texture2D tex = LoadTexture(getStream());
        tex.anisoLevel = 0;
        tex.filterMode = FilterMode.Point;
        HeavyTexturesCache.LoadAndCacheAtlasFromTexture(name, tex);
        return new(name);
    }

    /// <summary>
    /// Gets a <see cref="Texture2D"/> from a stream representing a PNG file.
    /// </summary>
    public static Texture2D LoadTexture(Stream stream)
    {
        byte[] imageData = new byte[stream.Length];

        stream.Read(imageData, 0, (int)stream.Length);

        Texture2D texture = new(4, 4);

        texture.LoadImage(imageData);

        return texture;
    }
}
