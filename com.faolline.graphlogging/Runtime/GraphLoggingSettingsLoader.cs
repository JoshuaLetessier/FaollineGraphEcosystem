using UnityEngine;

namespace Faolline.GraphLogging
{
    /// <summary>Loads the project-wide <see cref="GraphLoggingSettings"/> from Resources.</summary>
    public static class GraphLoggingSettingsLoader
    {
        private const string DefaultAssetName = "GraphLoggingSettings";

        public static GraphLoggingSettings Load() => Resources.Load<GraphLoggingSettings>(DefaultAssetName);

        public static string GetDefaultAssetPath() => $"Assets/Resources/{DefaultAssetName}.asset";
    }
}
