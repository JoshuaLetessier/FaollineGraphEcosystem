using NUnit.Framework;
using Faolline.GraphGameFlow.Editor;
using Faolline.GraphGameFlow.Addressables.Editor;

namespace Faolline.GraphGameFlow.Addressables.Tests
{
    /// <summary>
    /// Deterministic, EditMode-safe coverage, mirroring <c>AddressablesSceneLoaderTests</c>: interface
    /// compliance and the parts of <see cref="AddressablesSceneKeyProvider"/> that don't depend on a
    /// specific project Addressables content (which entries exist is environment state, not this class's
    /// behaviour).
    /// </summary>
    public class AddressablesSceneKeyProviderTests
    {
        [Test]
        public void ImplementsISceneKeySourceProvider()
        {
            Assert.IsInstanceOf<ISceneKeySourceProvider>(new AddressablesSceneKeyProvider());
        }

        [Test]
        public void SourceLabel_IsAddressable()
        {
            Assert.AreEqual("Addressable", new AddressablesSceneKeyProvider().SourceLabel);
        }

        [Test]
        public void CanPromote_AlwaysTrue()
        {
            var provider = new AddressablesSceneKeyProvider();
            Assert.IsTrue(provider.CanPromote("Assets/Scenes/Foo.unity", "Foo"));
        }

        [Test]
        public void GetKeys_NeverNull()
        {
            Assert.IsNotNull(new AddressablesSceneKeyProvider().GetKeys());
        }
    }
}
