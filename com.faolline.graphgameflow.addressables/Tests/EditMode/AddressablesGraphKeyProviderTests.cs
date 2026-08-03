using NUnit.Framework;
using Faolline.GraphGameFlow.Editor;
using Faolline.GraphGameFlow.Addressables.Editor;

namespace Faolline.GraphGameFlow.Addressables.Tests
{
    /// <summary>
    /// Mirrors <c>AddressablesSceneKeyProviderTests</c>: interface compliance and the parts that don't depend
    /// on specific project Addressables content (which entries exist is environment state, not this class's
    /// behaviour).
    /// </summary>
    public class AddressablesGraphKeyProviderTests
    {
        [Test]
        public void ImplementsIGraphKeySourceProvider()
        {
            Assert.IsInstanceOf<IGraphKeySourceProvider>(new AddressablesGraphKeyProvider());
        }

        [Test]
        public void SourceLabel_IsAddressable()
        {
            Assert.AreEqual("Addressable", new AddressablesGraphKeyProvider().SourceLabel);
        }

        [Test]
        public void CanPromote_AlwaysTrue()
        {
            var provider = new AddressablesGraphKeyProvider();
            Assert.IsTrue(provider.CanPromote("Assets/Graphs/Chapter2.asset", "chapter-2"));
        }

        [Test]
        public void GetKeys_NeverNull()
        {
            Assert.IsNotNull(new AddressablesGraphKeyProvider().GetKeys());
        }

        [Test]
        public void TryResolveGuid_UnknownGuid_ReturnsFalse()
        {
            var provider = new AddressablesGraphKeyProvider();
            Assert.IsFalse(provider.TryResolveGuid("00000000000000000000000000000000", out _));
        }
    }
}
