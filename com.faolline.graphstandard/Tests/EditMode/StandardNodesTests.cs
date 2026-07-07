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
            var score = ParameterName.Int("score");
            ctx.Set<int>(score, 5);
            var c = New<IntCondition>();
            c.Parameter = score; c.Operator = ComparisonOperator.GreaterOrEqual; c.ExpectedValue = 3;
            Assert.IsTrue(c.Evaluate(ctx), "5 >= 3");
            c.ExpectedValue = 9;
            Assert.IsFalse(c.Evaluate(ctx), "5 >= 9 is false");
            Object.DestroyImmediate(c); Object.DestroyImmediate(score);
        }

        [Test]
        public void IntCondition_MissingKey_IsFalse()
        {
            var absent = ParameterName.Int("absent");
            var c = New<IntCondition>();
            c.Parameter = absent;
            Assert.IsFalse(c.Evaluate(new BaseContext()));
            Object.DestroyImmediate(c); Object.DestroyImmediate(absent);
        }

        [Test]
        public void FloatCondition_Compares()
        {
            var ctx = new BaseContext();
            var ratio = ParameterName.Float("ratio");
            ctx.Set<float>(ratio, 0.3f);
            var c = New<FloatCondition>();
            c.Parameter = ratio; c.Operator = ComparisonOperator.Less; c.ExpectedValue = 0.5f;
            Assert.IsTrue(c.Evaluate(ctx));
            Object.DestroyImmediate(c); Object.DestroyImmediate(ratio);
        }

        [Test]
        public void BoolAndString_Conditions()
        {
            var ctx = new BaseContext();
            var open = ParameterName.Bool("open");
            var name = ParameterName.String("name");
            ctx.Set<bool>(open, true);
            ctx.Set<string>(name, "hero");

            var b = New<BoolCondition>(); b.Parameter = open; b.ExpectedValue = true;
            Assert.IsTrue(b.Evaluate(ctx));

            var s = New<StringCondition>(); s.Parameter = name; s.ExpectedValue = "villain";
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
            var pi = ParameterName.Int("i");
            var pf = ParameterName.Float("f");
            var ps = ParameterName.String("s");
            var pb = ParameterName.Bool("b");

            var setI = New<SetIntAction>();    setI.Parameter = pi; setI.Value = 7;    setI.Execute(ctx);
            var setF = New<SetFloatAction>();  setF.Parameter = pf; setF.Value = 1.5f; setF.Execute(ctx);
            var setS = New<SetStringAction>(); setS.Parameter = ps; setS.Value = "x";  setS.Execute(ctx);
            var setB = New<SetBoolAction>();   setB.Parameter = pb; setB.Value = true; setB.Execute(ctx);

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
