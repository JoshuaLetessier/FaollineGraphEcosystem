#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
namespace Faolline.GraphLocalization.Unity
{
    /// <summary>
    /// The one place <see cref="UnityLocalizedAssetProvider"/> opens an Asset Table Collection — a seam so tests can
    /// observe which collections a lookup opened.
    /// </summary>
    internal interface IAssetTableReader
    {
        /// <summary>
        /// True when <paramref name="collection"/> defines <paramref name="key"/>; <paramref name="asset"/> may be
        /// null when the key exists but is unassigned for the active locale.
        /// </summary>
        bool TryRead<T>(string collection, string key, out T asset) where T : UnityEngine.Object;
    }
}
#endif
