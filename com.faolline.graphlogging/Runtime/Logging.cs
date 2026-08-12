using UnityEngine;

namespace Faolline.GraphLogging
{
    /// <summary>
    /// Shared logging facade for the whole ecosystem: any package calls <see cref="Info"/>/
    /// <see cref="Warning"/> under its own category name (e.g. "GraphLocalization.AutoBuild") instead
    /// of <c>Debug.Log</c>/<c>Debug.LogWarning</c> directly, and the user can silence a category from
    /// <c>Faolline ▸ Diagnostics ▸ Log Settings</c> without touching lib source. No settings asset (or
    /// an unknown category) means "log everything" — adopting this facade never silently loses a
    /// message that used to show. <see cref="Error"/> is never gated: a real problem must always be
    /// visible, matching every other "fail loud" precedent in this ecosystem.
    /// </summary>
    public static class Logging
    {
        public static void Info(string category, string message)
        {
            var settings = GraphLoggingSettingsLoader.Load();
#if UNITY_EDITOR
            settings?.EnsureCategoryKnown(category);
#endif
            if (settings == null || settings.IsInfoEnabled(category))
                Debug.Log(message);
        }

        public static void Warning(string category, string message)
        {
            var settings = GraphLoggingSettingsLoader.Load();
#if UNITY_EDITOR
            settings?.EnsureCategoryKnown(category);
#endif
            if (settings == null || settings.IsWarningEnabled(category))
                Debug.LogWarning(message);
        }

        public static void Error(string category, string message)
        {
            var settings = GraphLoggingSettingsLoader.Load();
#if UNITY_EDITOR
            settings?.EnsureCategoryKnown(category);
#endif
            Debug.LogError(message);
        }
    }
}
