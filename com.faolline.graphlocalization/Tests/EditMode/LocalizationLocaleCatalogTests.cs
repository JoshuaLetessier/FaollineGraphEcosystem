using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>The editor locale catalog that backs language pickers (CSV columns; Unity locales when that mode).</summary>
    public class LocalizationLocaleCatalogTests
    {
        [Test]
        public void AvailableLocales_CsvMode_ReturnsConfiguredColumns()
        {
            var asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
            try
            {
                SetCsvLocales(asset, new[] { "en", "fr", "de" });
                var locales = new List<string>(LocalizationLocaleCatalog.AvailableLocales(asset));
                CollectionAssert.AreEqual(new[] { "en", "fr", "de" }, locales);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void AvailableLocales_CsvMode_DedupesAndDropsEmpties()
        {
            var asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
            try
            {
                SetCsvLocales(asset, new[] { "en", "", "fr", "en" });
                var locales = new List<string>(LocalizationLocaleCatalog.AvailableLocales(asset));
                CollectionAssert.AreEqual(new[] { "en", "fr" }, locales);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void AvailableLocales_NeverEmpty_FallsBackToEn()
        {
            var asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
            try
            {
                SetCsvLocales(asset, new string[0]);
                var locales = LocalizationLocaleCatalog.AvailableLocales(asset);
                Assert.AreEqual(1, locales.Count);
                Assert.AreEqual("en", locales[0]);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        // The CSV locale columns are a private serialized field; set them as the inspector would.
        private static void SetCsvLocales(LocalizationSettingsAsset asset, string[] codes)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("_csvLocales");
            prop.arraySize = codes.Length;
            for (int i = 0; i < codes.Length; i++)
                prop.GetArrayElementAtIndex(i).stringValue = codes[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
