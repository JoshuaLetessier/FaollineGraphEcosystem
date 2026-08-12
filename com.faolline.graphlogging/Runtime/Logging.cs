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
    ///
    /// The optional <paramref name="context"/> on every overload mirrors <c>Debug.Log(message, context)</c>
    /// — pass a <c>UnityEngine.Object</c> (typically <c>this</c> in a MonoBehaviour) to keep click-to-ping
    /// working in the console.
    /// </summary>
    public static class Logging
    {
        public static void Info(string category, string message, Object context = null)
        {
            var settings = GraphLoggingSettingsLoader.Load();
#if UNITY_EDITOR
            settings?.EnsureCategoryKnown(category);
#endif
            if (settings == null || settings.IsInfoEnabled(category))
                Debug.Log(message, context);
        }

        public static void Warning(string category, string message, Object context = null)
        {
            var settings = GraphLoggingSettingsLoader.Load();
#if UNITY_EDITOR
            settings?.EnsureCategoryKnown(category);
#endif
            if (settings == null || settings.IsWarningEnabled(category))
                Debug.LogWarning(message, context);
        }

        public static void Error(string category, string message, Object context = null)
        {
            var settings = GraphLoggingSettingsLoader.Load();
#if UNITY_EDITOR
            settings?.EnsureCategoryKnown(category);
#endif
            Debug.LogError(message, context);
        }
    }
}
