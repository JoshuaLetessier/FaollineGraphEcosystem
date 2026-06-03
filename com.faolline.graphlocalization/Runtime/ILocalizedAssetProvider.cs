using UnityEngine;

namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Resolves a localized **asset** (e.g. a voice clip) by the same key used for text, for the active
    /// locale. Lets a line's audio live in a Unity Localization Asset Table keyed identically to its text,
    /// instead of being assigned per node. Returns null when the key/asset is not available.
    /// </summary>
    public interface ILocalizedAssetProvider
    {
        T ResolveAsset<T>(string key) where T : Object;
    }
}
