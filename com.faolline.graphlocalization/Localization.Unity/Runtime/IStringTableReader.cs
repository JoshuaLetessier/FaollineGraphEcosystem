#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
namespace Faolline.GraphLocalization.Unity
{
    /// <summary>
    /// The one place <see cref="UnityLocalizationProvider"/> opens a String Table Collection. A seam so tests can
    /// observe exactly which collections a lookup opened (the point of table-targeted lookups).
    /// </summary>
    internal interface IStringTableReader
    {
        /// <summary>
        /// True when <paramref name="collection"/> defines <paramref name="key"/> (whether or not it is translated).
        /// <paramref name="value"/> is the selected-locale text, else the first non-empty text of any locale, else
        /// empty.
        /// </summary>
        bool TryRead(string collection, string key, out string value);
    }
}
#endif
