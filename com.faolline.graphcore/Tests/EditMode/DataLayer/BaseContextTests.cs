using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    public class BaseContextDataLayerTests
    {
        [Test]
        public void BaseContext_IsConcreteClass()
        {
            Assert.IsFalse(typeof(BaseContext).IsAbstract, "BaseContext must be a concrete class.");
        }

        [Test]
        public void BaseContext_IsNotScriptableObject()
        {
            Assert.IsFalse(
                typeof(UnityEngine.ScriptableObject).IsAssignableFrom(typeof(BaseContext)),
                "BaseContext must not be a ScriptableObject.");
        }

        [Test]
        public void BaseContext_CanBeSubclassed()
        {
            var ctx = new ConcreteContext();
            Assert.IsNotNull(ctx);
        }

        private class ConcreteContext : BaseContext { }
    }
}
