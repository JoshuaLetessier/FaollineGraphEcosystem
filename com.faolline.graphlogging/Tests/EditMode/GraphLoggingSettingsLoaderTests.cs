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

        [Test]
        public void Load_NoAssetInProject_ReturnsNull()
        {
            // No GraphLoggingSettings asset exists in this dev repo's Resources folder by default.
            Assert.IsNull(GraphLoggingSettingsLoader.Load());
        }
    }
}
