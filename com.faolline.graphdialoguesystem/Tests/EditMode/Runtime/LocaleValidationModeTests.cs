using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>P2a — per-locale validation mode configuration on the settings asset.</summary>
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
            // Permissive (silent) < Warn (default) < Strict (errors). Order matters for severity.
            Assert.AreEqual(0, (int)LocaleValidationMode.Permissive);
            Assert.AreEqual(1, (int)LocaleValidationMode.Warn);
            Assert.AreEqual(2, (int)LocaleValidationMode.Strict);
        }
    }
}
