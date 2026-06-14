using System;
using System.Collections.Generic;
using System.Reflection;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Editor catalog of the locale codes available to the project, resolved from the active
    /// <see cref="LocalizationSettingsAsset"/>: the Unity Localization locales (Project Settings &gt;
    /// Localization) when Mode = UnityLocalization, otherwise the CSV locale columns. Used to populate
    /// language pickers (e.g. the dialogue editor toolbar) with the real configured languages instead of
    /// free text. Never returns empty — a picker always has at least <c>"en"</c>.
    /// </summary>
    public static class LocalizationLocaleCatalog
    {
        /// <summary>
        /// The locale codes available for the active mode, in configured order (the first is the source /
        /// default where known). In UnityLocalization mode these are the Project Settings locales; otherwise
        /// (or if the Unity adapter is absent / has no locales) the CSV locale columns; finally a single
        /// <c>"en"</c> so the result is never empty.
        /// </summary>
        public static IReadOnlyList<string> AvailableLocales(LocalizationSettingsAsset settings = null)
        {
            settings = settings != null ? settings : LocalizationSettingsLoader.Load();

            if (settings != null && settings.Mode == LocalizationMode.UnityLocalization)
            {
                var unity = TryGetUnityLocaleCodes();
                if (unity != null && unity.Count > 0) return Dedup(unity);
                // Unity adapter missing or no locales configured → fall through to the CSV columns.
            }

            var csv = settings != null ? settings.CsvLocales : null;
            if (csv != null && csv.Count > 0)
            {
                var deduped = Dedup(csv);
                if (deduped.Count > 0) return deduped;
            }

            return new List<string> { "en" };
        }

        private static List<string> Dedup(IReadOnlyList<string> codes)
        {
            var list = new List<string>();
            foreach (var c in codes)
                if (!string.IsNullOrEmpty(c) && !list.Contains(c)) list.Add(c);
            return list;
        }

        // Reflects into the gated Unity Localization editor adapter (the same seam the table builder uses), so
        // the core editor needs no compile-time dependency on com.unity.localization.
        private static IReadOnlyList<string> TryGetUnityLocaleCodes()
        {
            var type = Type.GetType(
                "Faolline.GraphLocalization.Unity.Editor.UnityLocalizationSyncer, " +
                "com.faolline.graphlocalization.Localization.Unity.Editor");
            var method = type?.GetMethod("GetAvailableLocaleCodes", BindingFlags.Public | BindingFlags.Static);
            return method?.Invoke(null, null) as string[];
        }
    }
}
