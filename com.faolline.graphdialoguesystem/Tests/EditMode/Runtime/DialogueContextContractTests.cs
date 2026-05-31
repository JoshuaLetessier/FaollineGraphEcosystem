using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the DialogueContext typed contract (Constitution Principle VI).</summary>
    public class DialogueContextContractTests
    {
        [Test]
        public void TypedProperties_RoundTrip_ThroughKeys()
        {
            var ctx = new DialogueContext { Flag = true, Counter = 7, Amount = 1.5f, Tag = "hi" };

            Assert.IsTrue(ctx.Flag);
            Assert.AreEqual(7, ctx.Counter);
            Assert.AreEqual(1.5f, ctx.Amount);
            Assert.AreEqual("hi", ctx.Tag);

            // Values are stored under the centralized keys.
            Assert.IsTrue(ctx.TryGet<bool>(DialogueContextKeys.Flag, out var f) && f);
            Assert.IsTrue(ctx.TryGet<int>(DialogueContextKeys.Counter, out var c) && c == 7);
        }

        [Test]
        public void Defaults_AreSafe_WhenUnset()
        {
            var ctx = new DialogueContext();
            Assert.IsFalse(ctx.Flag);
            Assert.AreEqual(0, ctx.Counter);
            Assert.AreEqual(0f, ctx.Amount);
            Assert.AreEqual(string.Empty, ctx.Tag);
        }

        [Test]
        public void DeepClone_ReturnsDialogueContext_WithValues()
        {
            var ctx = new DialogueContext { Counter = 3, Tag = "x" };

            var clone = ctx.DeepClone();

            Assert.IsInstanceOf<DialogueContext>(clone);
            var typed = (DialogueContext)clone;
            Assert.AreEqual(3, typed.Counter);
            Assert.AreEqual("x", typed.Tag);
        }
    }
}
