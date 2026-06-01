using NUnit.Framework;
using Faolline.GraphLocalization;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>Unit tests for LocalizationException.</summary>
    public class LocalizationExceptionTests
    {
        [Test]
        public void Constructor_SetsKeyAndLocale()
        {
            var ex = new LocalizationException("line_abc", "fr");
            Assert.AreEqual("line_abc", ex.Key);
            Assert.AreEqual("fr", ex.Locale);
        }

        [Test]
        public void Message_ContainsKeyAndLocale()
        {
            var ex = new LocalizationException("line_abc", "fr");
            StringAssert.Contains("line_abc", ex.Message);
            StringAssert.Contains("fr", ex.Message);
        }

        [Test]
        public void IsException()
        {
            Assert.IsInstanceOf<System.Exception>(new LocalizationException("k", "en"));
        }
    }
}
