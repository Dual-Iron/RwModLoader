using UnityEngine;
using System.Collections;

namespace Realm.Assets;

// Credit goes to Henpemaz for this great snippet of code!
// https://gist.github.com/Dual-Iron/2481ac0528f3eedfa827ab7cfbef02ec

static class Asset
{
    /// <summary>
    /// Gets a sprite from this assembly's embedded resources.
    /// </summary>
    /// <param name="name">The name of the embedded resource.</param>
    /// <returns>The sprite represneting the embedded resource.</returns>
    public static FSprite GetSpriteFromRes(string name)
    {
        Stream stream = typeof(Program).Assembly.GetManifestResourceStream(name);
        Texture2D tex = LoadTexture(stream);
        tex.anisoLevel = 0;
        tex.filterMode = FilterMode.Point;
        HeavyTexturesCache.LoadAndCacheAtlasFromTexture(name, tex);
        return new(name);
    }

    /// <summary>
    /// Gets a <see cref="Texture2D"/> from a byte array representing a PNG file.
    /// </summary>
    /// <param name="pngBytes">The PNG as bytes.</param>
    /// <returns>The resulting Texture2D.</returns>
    public static Texture2D LoadTexture(byte[] pngBytes)
    {
        // "Empty" texture. Will be replaced by LoadImage
        Texture2D texture = new(4, 4);

        texture.LoadImage(pngBytes);

        return texture;
    }

    /// <summary>
    /// Gets a <see cref="Texture2D"/> from a stream representing a PNG file.
    /// </summary>
    /// <param name="pngBytes">The PNG as a stream.</param>
    /// <returns>The resulting Texture2D.</returns>
    public static Texture2D LoadTexture(Stream stream)
    {
        byte[] imageData = new byte[stream.Length];

        stream.Read(imageData, 0, (int)stream.Length);

        return LoadTexture(imageData);
    }

    /// <summary>
    /// Loads an atlas from a set of files.
    /// </summary>
    /// <param name="folder">The directory containing the files.</param>
    /// <param name="basename">The name of each file, excluding file extensions.</param>
    /// <param name="atlasName">The name of the atlas. If <see langword="null"/>, <paramref name="basename"/> will be used instead.</param>
    /// <returns></returns>
    public static FAtlas ReadAndLoadCustomAtlas(string folder, string basename, string? atlasName = null)
    {
        Debug.Log("CustomAtlasLoader: Loading atlas " + basename + " from " + folder);
        Texture2D imageData = new(0, 0, TextureFormat.ARGB32, false);
        imageData.LoadImage(File.ReadAllBytes(Path.Combine(folder, basename + ".png")));

        Dictionary<string, object>? slicerData = null;
        if (File.Exists(Path.Combine(folder, basename + ".txt"))) {
            Debug.Log("CustomAtlasLoader: found slicer data");
            slicerData = File.ReadAllText(Path.Combine(folder, basename + ".txt")).dictionaryFromJson();
        }

        Dictionary<string, string>? metaData = null;
        if (File.Exists(Path.Combine(folder, basename + ".png.meta"))) {
            Debug.Log("CustomAtlasLoader: found metadata");
            metaData = File.ReadAllLines(Path.Combine(folder, basename + ".png.meta")).ToList().ConvertAll(MetaEntryToKeyVal).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        return LoadCustomAtlas(atlasName ?? basename, imageData, slicerData, metaData);
    }

    /// <summary>
    /// Loads an atlas from streams. This method disposes the streams after reading from them.
    /// </summary>
    /// <param name="atlasName">The name of the atlas.</param>
    /// <param name="textureStream">The .png texture of the atlas.</param>
    /// <param name="slicerStream">The .txt stream that slices the atlas into sprites.</param>
    /// <param name="metaStream">The .png.meta stream that specifies miscellaneous metadata.</param>
    /// <returns></returns>
    public static FAtlas LoadCustomAtlas(string atlasName, Stream textureStream, Stream? slicerStream = null, Stream? metaStream = null)
    {
        using var _textureStream = textureStream;
        using var _slicerStream = slicerStream;
        using var _metaStream = metaStream;

        Texture2D imageData = new(0, 0, TextureFormat.ARGB32, false);
        byte[] bytes = new byte[_textureStream.Length];
        _textureStream.Read(bytes, 0, (int)_textureStream.Length);
        imageData.LoadImage(bytes);

        Dictionary<string, object>? slicerData = null;

        if (_slicerStream != null) {
            StreamReader sr = new(_slicerStream, Encoding.UTF8);
            slicerData = sr.ReadToEnd().dictionaryFromJson();
        }

        Dictionary<string, string>? metaData = null;

        if (_metaStream != null) {
            StreamReader sr = new(_metaStream, Encoding.UTF8);
            metaData = new Dictionary<string, string>();
            for (string fullLine = sr.ReadLine(); fullLine != null; fullLine = sr.ReadLine()) {
                (metaData as IDictionary<string, string>).Add(MetaEntryToKeyVal(fullLine));
            }
        }

        return LoadCustomAtlas(atlasName, imageData, slicerData, metaData);
    }

    private static KeyValuePair<string, string> MetaEntryToKeyVal(string input)
    {
        if (string.IsNullOrEmpty(input)) {
            return new("", "");
        }

        // No trim option in framework 3.5
        string[] pieces = input.Split(new char[] { ':' }, 2);
        if (pieces.Length == 0) {
            return new("", "");
        }

        if (pieces.Length == 1) {
            return new(pieces[0].Trim(), "");
        }

        return new(pieces[0].Trim(), pieces[1].Trim());
    }

    private static FAtlas LoadCustomAtlas(string atlasName, Texture2D imageData, Dictionary<string, object>? slicerData, Dictionary<string, string>? metaData)
    {
        // Some defaults, metadata can overwrite
        // common snense
        if (slicerData != null) {
            imageData.anisoLevel = 1;
            imageData.filterMode = 0;
        }
        else {
            imageData.wrapMode = TextureWrapMode.Clamp;
        }

        if (metaData != null) {
            metaData.TryGetValue("aniso", out string anisoValue);
            if (!string.IsNullOrEmpty(anisoValue) && int.Parse(anisoValue) > -1) {
                imageData.anisoLevel = int.Parse(anisoValue);
            }

            metaData.TryGetValue("filterMode", out string filterMode);
            if (!string.IsNullOrEmpty(filterMode) && int.Parse(filterMode) > -1) {
                imageData.filterMode = (FilterMode)int.Parse(filterMode);
            }

            metaData.TryGetValue("wrapMode", out string wrapMode);
            if (!string.IsNullOrEmpty(wrapMode) && int.Parse(wrapMode) > -1) {
                imageData.wrapMode = (TextureWrapMode)int.Parse(wrapMode);
            }
        }

        // make singleimage atlas
        FAtlas fatlas = new(atlasName, imageData, FAtlasManager._nextAtlasIndex);

        // was actually singleimage
        if (slicerData == null) {
            // Done
            if (Futile.atlasManager.DoesContainAtlas(atlasName)) {
                Debug.Log("Single-image atlas '" + atlasName + "' being replaced.");
                Futile.atlasManager.ActuallyUnloadAtlasOrImage(atlasName); // Unload previous version if present
            }
            if (Futile.atlasManager._allElementsByName.Remove(atlasName)) {
                Debug.Log("Element '" + atlasName + "' being replaced with new one from atlas " + atlasName);
            }

            FAtlasManager._nextAtlasIndex++; // is this guy even used
            Futile.atlasManager.AddAtlas(fatlas); // Simple
            return fatlas;
        }

        // convert to full atlas
        fatlas._elements.Clear();
        fatlas._elementsByName.Clear();
        fatlas._isSingleImage = false;


        //ctrl c
        //ctrl v

        Dictionary<string, object> dictionary2 = (Dictionary<string, object>)slicerData["frames"];
        float resourceScaleInverse = Futile.resourceScaleInverse;
        int num = 0;
        foreach (KeyValuePair<string, object> keyValuePair in dictionary2) {
            FAtlasElement fatlasElement = new();
            fatlasElement.indexInAtlas = num++;
            string text = keyValuePair.Key;
            if (Futile.shouldRemoveAtlasElementFileExtensions) {
                int num2 = text.LastIndexOf(".");
                if (num2 >= 0) {
                    text = text.Substring(0, num2);
                }
            }
            fatlasElement.name = text;
            IDictionary dictionary3 = (IDictionary)keyValuePair.Value;
            fatlasElement.isTrimmed = (bool)dictionary3["trimmed"];
            if ((bool)dictionary3["rotated"]) {
                throw new NotSupportedException("Futile no longer supports TexturePacker's \"rotated\" flag. Please disable it when creating the " + fatlas._dataPath + " atlas.");
            }
            IDictionary dictionary4 = (IDictionary)dictionary3["frame"];
            float num3 = float.Parse(dictionary4["x"].ToString());
            float num4 = float.Parse(dictionary4["y"].ToString());
            float num5 = float.Parse(dictionary4["w"].ToString());
            float num6 = float.Parse(dictionary4["h"].ToString());
            Rect uvRect = new(num3 / fatlas._textureSize.x, (fatlas._textureSize.y - num4 - num6) / fatlas._textureSize.y, num5 / fatlas._textureSize.x, num6 / fatlas._textureSize.y);
            fatlasElement.uvRect = uvRect;
            fatlasElement.uvTopLeft.Set(uvRect.xMin, uvRect.yMax);
            fatlasElement.uvTopRight.Set(uvRect.xMax, uvRect.yMax);
            fatlasElement.uvBottomRight.Set(uvRect.xMax, uvRect.yMin);
            fatlasElement.uvBottomLeft.Set(uvRect.xMin, uvRect.yMin);
            IDictionary dictionary5 = (IDictionary)dictionary3["sourceSize"];
            fatlasElement.sourcePixelSize.x = float.Parse(dictionary5["w"].ToString());
            fatlasElement.sourcePixelSize.y = float.Parse(dictionary5["h"].ToString());
            fatlasElement.sourceSize.x = fatlasElement.sourcePixelSize.x * resourceScaleInverse;
            fatlasElement.sourceSize.y = fatlasElement.sourcePixelSize.y * resourceScaleInverse;
            IDictionary dictionary6 = (IDictionary)dictionary3["spriteSourceSize"];
            float left = float.Parse(dictionary6["x"].ToString()) * resourceScaleInverse;
            float top = float.Parse(dictionary6["y"].ToString()) * resourceScaleInverse;
            float width = float.Parse(dictionary6["w"].ToString()) * resourceScaleInverse;
            float height = float.Parse(dictionary6["h"].ToString()) * resourceScaleInverse;
            fatlasElement.sourceRect = new Rect(left, top, width, height);
            fatlas._elements.Add(fatlasElement);
            fatlas._elementsByName.Add(fatlasElement.name, fatlasElement);
        }

        // This currently doesn't remove elements from old atlases, just removes elements from the manager.
        bool nameInUse = Futile.atlasManager.DoesContainAtlas(atlasName);
        if (!nameInUse) {
            // remove duplicated elements and add atlas
            foreach (FAtlasElement fae in fatlas._elements) {
                if (Futile.atlasManager._allElementsByName.Remove(fae.name)) {
                    Debug.Log("Element '" + fae.name + "' being replaced with new one from atlas " + atlasName);
                }
            }
            FAtlasManager._nextAtlasIndex++;
            Futile.atlasManager.AddAtlas(fatlas);
        }
        else {
            FAtlas other = Futile.atlasManager.GetAtlasWithName(atlasName);
            bool isFullReplacement = true;
            foreach (FAtlasElement fae in other.elements) {
                if (!fatlas._elementsByName.ContainsKey(fae.name)) {
                    isFullReplacement = false;
                }
            }
            if (isFullReplacement) {
                // Done, we're good, unload the old and load the new
                Debug.Log("Atlas '" + atlasName + "' being fully replaced with custom one");
                Futile.atlasManager.ActuallyUnloadAtlasOrImage(atlasName); // Unload previous version if present
                FAtlasManager._nextAtlasIndex++;
                Futile.atlasManager.AddAtlas(fatlas); // Simple
            }
            else {
                // uuuugh
                // partially unload the old
                foreach (FAtlasElement fae in fatlas._elements) {
                    if (Futile.atlasManager._allElementsByName.Remove(fae.name)) {
                        Debug.Log("Element '" + fae.name + "' being replaced with new one from atlas " + atlasName);
                    }
                }
                // load the new with a salted name
                do {
                    atlasName += UnityEngine.Random.Range(0, 9);
                }
                while (Futile.atlasManager.DoesContainAtlas(atlasName));
                fatlas._name = atlasName;
                FAtlasManager._nextAtlasIndex++;
                Futile.atlasManager.AddAtlas(fatlas); // Finally
            }
        }
        return fatlas;
    }
}
