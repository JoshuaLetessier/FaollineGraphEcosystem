using NUnit.Framework;
using UnityEngine;
using Faolline.GraphLocalization;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>Per-locale validation mode configuration on the settings asset.</summary>
    public class LocaleValidationModeTests
    {
        [Test]
        public void LocalizationSettingsAsset_DefaultsToWarn()
        {
            var asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
            try
            {
                Assert.AreEqual(LocaleValidationMode.Warn, asset.LocaleValidation,
                    "Default validation mode should be Warn (catches gaps, never blocks).");
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void Enum_HasThreeTiers()
        {
            Assert.AreEqual(0, (int)LocaleValidationMode.Permissive);
            Assert.AreEqual(1, (int)LocaleValidationMode.Warn);
            Assert.AreEqual(2, (int)LocaleValidationMode.Strict);
        }
    }
}
