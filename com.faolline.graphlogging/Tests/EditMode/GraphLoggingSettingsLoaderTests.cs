using NUnit.Framework;

namespace Faolline.GraphLogging.Tests
{
    public class GraphLoggingSettingsLoaderTests
    {
        [Test]
        public void GetDefaultAssetPath_IsUnderAssetsResources()
        {
            Assert.AreEqual("Assets/Resources/GraphLoggingSettings.asset", GraphLoggingSettingsLoader.GetDefaultAssetPath());
        }

        // No "Load() returns null when no asset exists" test: whether this dev repo's own
        // Assets/Resources/GraphLoggingSettings.asset exists is ambient, real-usage-driven state (e.g.
        // opening Faolline ▸ Diagnostics ▸ Log Settings creates it) — not something a test should assert
        // on. graphlocalization's equivalent LocalizationSettingsLoader has no such test either.
    }
}
