#if GRAPHLOCALIZATION_UNITY_LOCALIZATION
using UnityEngine.Localization.Tables;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphLocalization.Unity
{
    /// <summary>Reads Asset Table Collections through Unity Localization's asset database.</summary>
    internal sealed class UnityAssetTableReader : IAssetTableReader
    {
        public bool TryRead<T>(string collection, string key, out T asset) where T : UnityEngine.Object
        {
            asset = null;
            if (string.IsNullOrEmpty(collection)) return false;
            try
            {
                var db = UnityLocalizationSettings.AssetDatabase;
                if (db == null) return false;
                var table = db.GetTableAsync(collection).WaitForCompletion() as AssetTable;
                var shared = table != null ? table.SharedData : null;
                if (shared == null) return false;
                if (shared.GetEntry(key) == null) return false; // key not defined in this collection

                asset = db.GetLocalizedAssetAsync<T>(collection, key).WaitForCompletion();
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
