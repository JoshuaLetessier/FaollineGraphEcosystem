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
            var flag = VariableDef.Bool("flag");
            ctx.Set<bool>(flag, true);
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.Variable = flag; c.ExpectedValue = true;
            try
            {
                Assert.IsTrue(c.Evaluate(ctx));
                c.ExpectedValue = false;
                Assert.IsFalse(c.Evaluate(ctx));
            }
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(flag); }
        }

        [Test]
        public void BoolCondition_AbsentKey_IsFalse_SilentlyByDefault()
        {
            var missing = VariableDef.Bool("missing");
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.Variable = missing; c.ExpectedValue = true;
            try { Assert.IsFalse(c.Evaluate(new BaseContext())); }   // WarnOnMissing defaults false → silent false
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(missing); }
        }

        [Test]
        public void SetBoolAction_WritesValueIntoContext()
        {
            var ctx = new BaseContext();
            var x = VariableDef.Bool("x");
            var a = ScriptableObject.CreateInstance<SetBoolAction>();
            a.Variable = x; a.Value = true;
            try
            {
                a.Execute(ctx);
                Assert.IsTrue(ctx.TryGet<bool>(x, out var v) && v, "the action wrote the bool into the context.");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(x); }
        }
    }
}
