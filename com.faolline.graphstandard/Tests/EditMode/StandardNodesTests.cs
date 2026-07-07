using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>Domain-neutral standard nodes (set/compare/log) promoted into graphstandard.</summary>
    public class StandardNodesTests
    {
        private static T New<T>() where T : ScriptableObject => ScriptableObject.CreateInstance<T>();

        [Test]
        public void IntCondition_ComparesViaOperator()
        {
            var ctx = new BaseContext();
            var score = VariableDef.Int("score");
            ctx.Set<int>(score, 5);
            var c = New<IntCondition>();
            c.Variable = score; c.Operator = ComparisonOperator.GreaterOrEqual; c.ExpectedValue = 3;
            Assert.IsTrue(c.Evaluate(ctx), "5 >= 3");
            c.ExpectedValue = 9;
            Assert.IsFalse(c.Evaluate(ctx), "5 >= 9 is false");
            Object.DestroyImmediate(c); Object.DestroyImmediate(score);
        }

        [Test]
        public void IntCondition_MissingKey_IsFalse()
        {
            var absent = VariableDef.Int("absent");
            var c = New<IntCondition>();
            c.Variable = absent;
            Assert.IsFalse(c.Evaluate(new BaseContext()));
            Object.DestroyImmediate(c); Object.DestroyImmediate(absent);
        }

        [Test]
        public void FloatCondition_Compares()
        {
            var ctx = new BaseContext();
            var ratio = VariableDef.Float("ratio");
            ctx.Set<float>(ratio, 0.3f);
            var c = New<FloatCondition>();
            c.Variable = ratio; c.Operator = ComparisonOperator.Less; c.ExpectedValue = 0.5f;
            Assert.IsTrue(c.Evaluate(ctx));
            Object.DestroyImmediate(c); Object.DestroyImmediate(ratio);
        }

        [Test]
        public void BoolAndString_Conditions()
        {
            var ctx = new BaseContext();
            var open = VariableDef.Bool("open");
            var name = VariableDef.String("name");
            ctx.Set<bool>(open, true);
            ctx.Set<string>(name, "hero");

            var b = New<BoolCondition>(); b.Variable = open; b.ExpectedValue = true;
            Assert.IsTrue(b.Evaluate(ctx));

            var s = New<StringCondition>(); s.Variable = name; s.ExpectedValue = "villain";
            Assert.IsFalse(s.Evaluate(ctx), "hero != villain");
            s.Negate = true;
            Assert.IsTrue(s.Evaluate(ctx), "negate flips inequality to pass");

            Object.DestroyImmediate(b); Object.DestroyImmediate(s);
            Object.DestroyImmediate(open); Object.DestroyImmediate(name);
        }

        [Test]
        public void AlwaysTrueFalse_Conditions()
        {
            var t = New<AlwaysTrueCondition>();
            var f = New<AlwaysFalseCondition>();
            Assert.IsTrue(t.Evaluate(new BaseContext()));
            Assert.IsFalse(f.Evaluate(new BaseContext()));
            Object.DestroyImmediate(t); Object.DestroyImmediate(f);
        }

        [Test]
        public void SetActions_WriteContext_ReadBackByCondition()
        {
            var ctx = new BaseContext();
            var pi = VariableDef.Int("i");
            var pf = VariableDef.Float("f");
            var ps = VariableDef.String("s");
            var pb = VariableDef.Bool("b");

            var setI = New<SetIntAction>();    setI.Variable = pi; setI.Value = 7;    setI.Execute(ctx);
            var setF = New<SetFloatAction>();  setF.Variable = pf; setF.Value = 1.5f; setF.Execute(ctx);
            var setS = New<SetStringAction>(); setS.Variable = ps; setS.Value = "x";  setS.Execute(ctx);
            var setB = New<SetBoolAction>();   setB.Variable = pb; setB.Value = true; setB.Execute(ctx);

            Assert.IsTrue(ctx.TryGet<int>(pi, out var i) && i == 7);
            Assert.IsTrue(ctx.TryGet<float>(pf, out var f) && Mathf.Approximately(f, 1.5f));
            Assert.IsTrue(ctx.TryGet<string>(ps, out var s) && s == "x");
            Assert.IsTrue(ctx.TryGet<bool>(pb, out var b) && b);

            Object.DestroyImmediate(setI); Object.DestroyImmediate(setF);
            Object.DestroyImmediate(setS); Object.DestroyImmediate(setB);
            Object.DestroyImmediate(pi); Object.DestroyImmediate(pf);
            Object.DestroyImmediate(ps); Object.DestroyImmediate(pb);
        }
    }
}
