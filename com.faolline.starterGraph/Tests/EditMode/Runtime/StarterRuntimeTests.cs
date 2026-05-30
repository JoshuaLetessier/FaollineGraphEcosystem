using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;

namespace Faolline.StarterGraph.Tests
{
    /// <summary>US1 — graph type, choice, typed conditions and actions.</summary>
    [TestFixture]
    public class StarterRuntimeTests
    {
        // ── Graph ──────────────────────────────────────────────────────────────
        [Test]
        public void StarterGraph_IsBaseGraph_WithCreateAssetMenu()
        {
            Assert.IsTrue(typeof(BaseGraph).IsAssignableFrom(typeof(StarterGraph)));
            Assert.IsNotEmpty(typeof(StarterGraph).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false));
        }

        // ── Choice ─────────────────────────────────────────────────────────────
        [Test]
        public void StarterChoice_IsBaseChoice_Serializable_WithLabel()
        {
            Assert.IsTrue(typeof(BaseChoice).IsAssignableFrom(typeof(StarterChoice)));
            Assert.IsNotEmpty(typeof(StarterChoice).GetCustomAttributes(typeof(SerializableAttribute), false));
            var c = new StarterChoice { Id = "x", Label = "Go" };
            Assert.AreEqual("Go", c.Label);
            Assert.AreEqual("x", c.Id);
        }

        // ── Conditions ─────────────────────────────────────────────────────────
        private static BaseContext CtxInt(string k, int v)    { var c = new BaseContext(); c.Set<int>(k, v);    return c; }
        private static BaseContext CtxFloat(string k, float v){ var c = new BaseContext(); c.Set<float>(k, v);  return c; }
        private static BaseContext CtxStr(string k, string v) { var c = new BaseContext(); c.Set<string>(k, v); return c; }

        [Test]
        public void AlwaysTrueFalse_Conditions_Work()
        {
            var t = ScriptableObject.CreateInstance<StarterAlwaysTrueCondition>();
            var f = ScriptableObject.CreateInstance<StarterAlwaysFalseCondition>();
            try { Assert.IsTrue(t.Evaluate(new BaseContext())); Assert.IsFalse(f.Evaluate(new BaseContext())); }
            finally { UnityEngine.Object.DestroyImmediate(t); UnityEngine.Object.DestroyImmediate(f); }
        }

        [Test]
        public void IntCondition_Operator_And_NullSafe()
        {
            var c = ScriptableObject.CreateInstance<StarterIntCondition>();
            c.ParameterKey = "score"; c.Operator = ComparisonOperator.GreaterOrEqual; c.ExpectedValue = 3;
            try
            {
                Assert.IsTrue(c.Evaluate(CtxInt("score", 5)));
                Assert.IsFalse(c.Evaluate(CtxInt("score", 1)));
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not found"));
                Assert.IsFalse(c.Evaluate(new BaseContext()));
            }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }

        [Test]
        public void FloatCondition_Less_Works()
        {
            var c = ScriptableObject.CreateInstance<StarterFloatCondition>();
            c.ParameterKey = "r"; c.Operator = ComparisonOperator.Less; c.ExpectedValue = 0.5f;
            try { Assert.IsTrue(c.Evaluate(CtxFloat("r", 0.2f))); Assert.IsFalse(c.Evaluate(CtxFloat("r", 0.9f))); }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }

        [Test]
        public void StringCondition_EqualityAndNegate()
        {
            var c = ScriptableObject.CreateInstance<StarterStringCondition>();
            c.ParameterKey = "n"; c.ExpectedValue = "hero"; c.Negate = false;
            try
            {
                Assert.IsTrue(c.Evaluate(CtxStr("n", "hero")));
                Assert.IsFalse(c.Evaluate(CtxStr("n", "x")));
                c.Negate = true;
                Assert.IsTrue(c.Evaluate(CtxStr("n", "x")));
            }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }

        [Test]
        public void BoolCondition_Works()
        {
            var c = ScriptableObject.CreateInstance<StarterBoolCondition>();
            c.ParameterKey = "b"; c.ExpectedValue = true;
            var ctx = new BaseContext(); ctx.Set<bool>("b", true);
            try { Assert.IsTrue(c.Evaluate(ctx)); }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }

        // ── Actions ────────────────────────────────────────────────────────────
        [Test]
        public void SetActions_WriteTypedValues()
        {
            var ai = ScriptableObject.CreateInstance<StarterSetIntAction>();    ai.ParameterKey = "i"; ai.Value = 7;
            var af = ScriptableObject.CreateInstance<StarterSetFloatAction>();  af.ParameterKey = "f"; af.Value = 1.5f;
            var asg= ScriptableObject.CreateInstance<StarterSetStringAction>(); asg.ParameterKey = "s"; asg.Value = "hi";
            var ab = ScriptableObject.CreateInstance<StarterSetBoolAction>();   ab.ParameterKey = "b"; ab.Value = true;
            var ctx = new BaseContext();
            try
            {
                ai.Execute(ctx); af.Execute(ctx); asg.Execute(ctx); ab.Execute(ctx);
                Assert.IsTrue(ctx.TryGet<int>("i", out var iv) && iv == 7);
                Assert.IsTrue(ctx.TryGet<float>("f", out var fv)); Assert.AreEqual(1.5f, fv, 0.0001f);
                Assert.IsTrue(ctx.TryGet<string>("s", out var sv) && sv == "hi");
                Assert.IsTrue(ctx.TryGet<bool>("b", out var bv) && bv);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ai); UnityEngine.Object.DestroyImmediate(af);
                UnityEngine.Object.DestroyImmediate(asg); UnityEngine.Object.DestroyImmediate(ab);
            }
        }
    }
}
