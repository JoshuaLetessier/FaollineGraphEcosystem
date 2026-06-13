using NUnit.Framework;
using Faolline.GraphCore;

namespace Faolline.StarterGraph.Tests
{
    /// <summary>The typed-context contract (Principle VI): typed props + clone subtype (history-restore safety).</summary>
    [TestFixture]
    public class StarterContextContractTests
    {
        [Test]
        public void TypedProperties_RoundTrip()
        {
            var ctx = new StarterContext { Flag = true, Score = 7, Ratio = 0.25f, Label = "hero" };
            Assert.IsTrue(ctx.Flag);
            Assert.AreEqual(7, ctx.Score);
            Assert.AreEqual(0.25f, ctx.Ratio, 0.0001f);
            Assert.AreEqual("hero", ctx.Label);
        }

        [Test]
        public void DeepClone_ReturnsSubtype_WithValues()
        {
            var ctx = new StarterContext { Flag = true, Score = 9, Ratio = 1.5f, Label = "x" };
            var clone = ctx.DeepClone();
            Assert.IsInstanceOf<StarterContext>(clone,
                "CreateCloneInstance() must return StarterContext, else GoBack history restore breaks");
            var typed = (StarterContext)clone;
            Assert.IsTrue(typed.Flag);
            Assert.AreEqual(9, typed.Score);
            Assert.AreEqual(1.5f, typed.Ratio, 0.0001f);
            Assert.AreEqual("x", typed.Label);
        }
    }
}
