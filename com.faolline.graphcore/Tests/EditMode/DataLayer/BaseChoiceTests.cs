using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    public class BaseChoiceTests
    {
        [Test]
        public void BaseChoice_IsSerializable()
        {
            var attrs = typeof(BaseChoice).GetCustomAttributes(
                typeof(System.SerializableAttribute), false);
            Assert.IsTrue(attrs.Length > 0, "BaseChoice must be marked [Serializable].");
        }

        [Test]
        public void BaseChoice_IsNotSealed()
        {
            Assert.IsFalse(typeof(BaseChoice).IsSealed,
                "BaseChoice must not be sealed so libs can subclass it.");
        }

        [Test]
        public void BaseChoice_Id_GetSet()
        {
            var choice = new BaseChoice { Id = "test-guid" };
            Assert.AreEqual("test-guid", choice.Id);
        }

        [Test]
        public void BaseChoice_Condition_IsNullByDefault()
        {
            var choice = new BaseChoice();
            Assert.IsNull(choice.Condition, "Condition must be null by default.");
        }
    }
}
