using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class ParamCompareConditionTests
    {
        private BaseContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _ctx = new BaseContext();
            _ctx.Set<int>("hp", 30);
            _ctx.Set<int>("hpMax", 100);
            _ctx.Set<float>("speed", 5.5f);
            _ctx.Set<float>("maxSpeed", 10f);
            _ctx.Set<string>("name", "Alice");
            _ctx.Set<string>("target", "Bob");
        }

        // ── IntCompare ────────────────────────────────────────────────────────

        [Test]
        public void IntCompare_LessThan()
        {
            var c = ScriptableObject.CreateInstance<IntCompareCondition>();
            c.LeftKey = "hp"; c.Operator = ComparisonOperator.Less; c.RightKey = "hpMax";
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void IntCompare_Equal_SameKey()
        {
            var c = ScriptableObject.CreateInstance<IntCompareCondition>();
            c.LeftKey = "hp"; c.Operator = ComparisonOperator.Equal; c.RightKey = "hp";
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void IntCompare_AbsentKeys_BothZero_Equal()
        {
            var c = ScriptableObject.CreateInstance<IntCompareCondition>();
            c.LeftKey = "missing1"; c.Operator = ComparisonOperator.Equal; c.RightKey = "missing2";
            try { Assert.IsTrue(c.Evaluate(new BaseContext())); }
            finally { Object.DestroyImmediate(c); }
        }

        // ── FloatCompare ──────────────────────────────────────────────────────

        [Test]
        public void FloatCompare_GreaterOrEqual()
        {
            var c = ScriptableObject.CreateInstance<FloatCompareCondition>();
            c.LeftKey = "maxSpeed"; c.Operator = ComparisonOperator.GreaterOrEqual; c.RightKey = "speed";
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void FloatCompare_NotEqual()
        {
            var c = ScriptableObject.CreateInstance<FloatCompareCondition>();
            c.LeftKey = "speed"; c.Operator = ComparisonOperator.NotEqual; c.RightKey = "maxSpeed";
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        // ── StringCompare ─────────────────────────────────────────────────────

        [Test]
        public void StringCompare_Equal_SameValue()
        {
            _ctx.Set<string>("target", "Alice");
            var c = ScriptableObject.CreateInstance<StringCompareCondition>();
            c.LeftKey = "name"; c.ExpectEqual = true; c.RightKey = "target";
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void StringCompare_NotEqual_DifferentValues()
        {
            var c = ScriptableObject.CreateInstance<StringCompareCondition>();
            c.LeftKey = "name"; c.ExpectEqual = false; c.RightKey = "target";
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void StringCompare_AbsentKeys_BothEmpty_Equal()
        {
            var c = ScriptableObject.CreateInstance<StringCompareCondition>();
            c.LeftKey = "x"; c.ExpectEqual = true; c.RightKey = "y";
            try { Assert.IsTrue(c.Evaluate(new BaseContext())); }
            finally { Object.DestroyImmediate(c); }
        }
    }
}
