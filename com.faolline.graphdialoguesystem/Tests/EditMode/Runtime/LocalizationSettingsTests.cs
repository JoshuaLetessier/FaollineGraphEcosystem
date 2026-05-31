using NUnit.Framework;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for LocalizationSettings safe-default behavior.</summary>
    public class LocalizationSettingsTests
    {
        [Test]
        public void Provider_IsNeverNull_WhenUnconfigured()
        {
            var settings = new LocalizationSettings();
            Assert.IsNotNull(settings.Provider);
        }

        [Test]
        public void Resolve_UsesActiveProviderAndLocale()
        {
            var settings = new LocalizationSettings
            {
                Provider = new CsvLocalizationProvider("Key,en,fr\ndlg.hi,Hi,Salut\n", "en"),
                CurrentLocale = "fr"
            };
            Assert.AreEqual("Salut", settings.Resolve("dlg.hi"));
        }

        [Test]
        public void CurrentLocale_RejectsEmpty()
        {
            var settings = new LocalizationSettings { CurrentLocale = "fr" };
            settings.CurrentLocale = "";
            Assert.AreEqual("fr", settings.CurrentLocale);
        }
    }
}
