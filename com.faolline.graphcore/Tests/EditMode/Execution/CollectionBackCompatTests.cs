using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Collections are global-only (ignore the 0.3.0 local-context overlay) and a context using no
    /// collections behaves identically to 0.4.0 (the non-breakage gate at the context layer).
    /// </summary>
    public class CollectionBackCompatTests
    {
        [Test]
        public void Collections_AreGlobal_SurviveEndLocalContext()
        {
            var ctx = new BaseContext();
            ctx.BeginLocalContext();
            ctx.AddToCollection("solved", "p1");
            ctx.EndLocalContext();

            Assert.IsTrue(ctx.CollectionContains("solved", "p1"),
                "Collections are global — not discarded when a local context ends.");
        }

        [Test]
        public void OpeningLocalContext_DoesNotBranchCollections()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            ctx.BeginLocalContext();
            Assert.IsTrue(ctx.CollectionContains("k", "a"), "Global members are visible inside a local scope.");
            ctx.AddToCollection("k", "b");
            ctx.EndLocalContext();
            Assert.AreEqual(2, ctx.CollectionCount("k"), "Both writes persist globally.");
        }

        [Test]
        public void NoCollections_BehavesLikeBefore()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("x", 1);
            Assert.AreEqual(1, ctx.GetAllParameters().Count);
            Assert.AreEqual(0, ctx.GetAllCollections().Count);
        }
    }
}
