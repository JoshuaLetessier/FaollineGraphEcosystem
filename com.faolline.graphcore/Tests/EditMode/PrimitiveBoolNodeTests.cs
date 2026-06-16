using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>The primitive bool condition/action canonical to GraphCore (downstream libs subclass these).</summary>
    public class PrimitiveBoolNodeTests
    {
        [Test]
        public void BoolCondition_ComparesAgainstExpected()
        {
            var ctx = new BaseContext();
            ctx.Set<bool>("flag", true);
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.ParameterKey = "flag"; c.ExpectedValue = true;
            try
            {
                Assert.IsTrue(c.Evaluate(ctx));
                c.ExpectedValue = false;
                Assert.IsFalse(c.Evaluate(ctx));
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void BoolCondition_AbsentKey_IsFalse_SilentlyByDefault()
        {
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.ParameterKey = "missing"; c.ExpectedValue = true;
            try { Assert.IsFalse(c.Evaluate(new BaseContext())); }   // WarnOnMissing defaults false → silent false
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void SetBoolAction_WritesValueIntoContext()
        {
            var ctx = new BaseContext();
            var a = ScriptableObject.CreateInstance<SetBoolAction>();
            a.ParameterKey = "x"; a.Value = true;
            try
            {
                a.Execute(ctx);
                Assert.IsTrue(ctx.TryGet<bool>("x", out var v) && v, "the action wrote the bool into the context.");
            }
            finally { Object.DestroyImmediate(a); }
        }
    }
}
