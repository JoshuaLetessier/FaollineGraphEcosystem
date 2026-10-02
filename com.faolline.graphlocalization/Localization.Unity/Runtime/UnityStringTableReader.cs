#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphLocalization.Unity
{
    /// <summary>Reads String Table Collections through Unity Localization's string database.</summary>
    internal sealed class UnityStringTableReader : IStringTableReader
    {
        public bool TryRead(string collection, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(collection)) return false;
            try
            {
                var db = UnityLocalizationSettings.StringDatabase;
                if (db == null) return false;
                var table = db.GetTableAsync(collection).WaitForCompletion();
                var shared = table != null ? table.SharedData : null;
                if (shared == null) return false;

                var sharedEntry = shared.GetEntry(key);
                if (sharedEntry == null) return false; // key not defined in this collection

                // Selected locale first.
                var selected = table.GetEntry(sharedEntry.Id);
                var selectedValue = selected != null ? selected.GetLocalizedString() : null;
                if (!string.IsNullOrEmpty(selectedValue)) { value = selectedValue; return true; }

                // Graceful fallback: any locale with a non-empty value (typically the source text).
                var locales = UnityLocalizationSettings.AvailableLocales != null
                    ? UnityLocalizationSettings.AvailableLocales.Locales : null;
                if (locales != null)
                {
                    foreach (var loc in locales)
                    {
                        if (loc == null) continue;
                        var localeTable = db.GetTableAsync(collection, loc).WaitForCompletion();
                        var entry = localeTable != null ? localeTable.GetEntry(sharedEntry.Id) : null;
                        var localeValue = entry != null ? entry.GetLocalizedString() : null;
                        if (!string.IsNullOrEmpty(localeValue)) { value = localeValue; return true; }
                    }
                }

                value = string.Empty; // key exists but untranslated everywhere
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
