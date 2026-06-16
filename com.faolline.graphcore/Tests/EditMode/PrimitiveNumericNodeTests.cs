using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>The numeric/string primitive conditions canonical to GraphCore — operator comparison + silent absence.</summary>
    public class PrimitiveNumericNodeTests
    {
        [Test]
        public void IntCondition_AppliesTheComparisonOperator()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("hp", 5);
            var c = ScriptableObject.CreateInstance<IntCondition>();
            c.ParameterKey = "hp"; c.ExpectedValue = 3;
            try
            {
                c.Operator = ComparisonOperator.Greater;        Assert.IsTrue(c.Evaluate(ctx));
                c.Operator = ComparisonOperator.LessOrEqual;    Assert.IsFalse(c.Evaluate(ctx));
                c.Operator = ComparisonOperator.Equal;          Assert.IsFalse(c.Evaluate(ctx));
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void FloatCondition_AbsentKey_IsFalse_SilentlyByDefault()
        {
            var c = ScriptableObject.CreateInstance<FloatCondition>();
            c.ParameterKey = "missing"; c.ExpectedValue = 1f;
            try { Assert.IsFalse(c.Evaluate(new BaseContext())); }   // WarnOnMissing defaults false → silent
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void StringCondition_EqualityAndNegate()
        {
            var ctx = new BaseContext();
            ctx.Set<string>("name", "keep");
            var c = ScriptableObject.CreateInstance<StringCondition>();
            c.ParameterKey = "name"; c.ExpectedValue = "keep";
            try
            {
                Assert.IsTrue(c.Evaluate(ctx));
                c.Negate = true;
                Assert.IsFalse(c.Evaluate(ctx));
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void SetIntAction_WritesValue()
        {
            var ctx = new BaseContext();
            var a = ScriptableObject.CreateInstance<SetIntAction>();
            a.ParameterKey = "n"; a.Value = 7;
            try
            {
                a.Execute(ctx);
                Assert.IsTrue(ctx.TryGet<int>("n", out var v) && v == 7);
            }
            finally { Object.DestroyImmediate(a); }
        }
    }
}
