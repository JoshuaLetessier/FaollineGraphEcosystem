using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class ParamCompareConditionTests
    {
        private BaseContext _ctx;

        // Governed parameters are ParameterName assets keyed by GUID (islands): the context value and the
        // condition reference must use the SAME instance to interoperate. Created per-test, destroyed in TearDown.
        private readonly List<Object> _created = new List<Object>();
        private ParameterName _hp, _hpMax, _speed, _maxSpeed, _name, _target;

        private ParameterName P(ParameterName p) { _created.Add(p); return p; }

        [SetUp]
        public void SetUp()
        {
            _ctx = new BaseContext();
            _hp       = P(ParameterName.Int("hp"));
            _hpMax    = P(ParameterName.Int("hpMax"));
            _speed    = P(ParameterName.Float("speed"));
            _maxSpeed = P(ParameterName.Float("maxSpeed"));
            _name     = P(ParameterName.String("name"));
            _target   = P(ParameterName.String("target"));

            _ctx.Set<int>(_hp, 30);
            _ctx.Set<int>(_hpMax, 100);
            _ctx.Set<float>(_speed, 5.5f);
            _ctx.Set<float>(_maxSpeed, 10f);
            _ctx.Set<string>(_name, "Alice");
            _ctx.Set<string>(_target, "Bob");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ── IntCompare ────────────────────────────────────────────────────────

        [Test]
        public void IntCompare_LessThan()
        {
            var c = ScriptableObject.CreateInstance<IntCompareCondition>();
            c.Left = _hp; c.Operator = ComparisonOperator.Less; c.Right = _hpMax;
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void IntCompare_Equal_SameKey()
        {
            var c = ScriptableObject.CreateInstance<IntCompareCondition>();
            c.Left = _hp; c.Operator = ComparisonOperator.Equal; c.Right = _hp;
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void IntCompare_AbsentKeys_BothZero_Equal()
        {
            var missing1 = P(ParameterName.Int("missing1"));
            var missing2 = P(ParameterName.Int("missing2"));
            var c = ScriptableObject.CreateInstance<IntCompareCondition>();
            c.Left = missing1; c.Operator = ComparisonOperator.Equal; c.Right = missing2;
            try { Assert.IsTrue(c.Evaluate(new BaseContext())); }
            finally { Object.DestroyImmediate(c); }
        }

        // ── FloatCompare ──────────────────────────────────────────────────────

        [Test]
        public void FloatCompare_GreaterOrEqual()
        {
            var c = ScriptableObject.CreateInstance<FloatCompareCondition>();
            c.Left = _maxSpeed; c.Operator = ComparisonOperator.GreaterOrEqual; c.Right = _speed;
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void FloatCompare_NotEqual()
        {
            var c = ScriptableObject.CreateInstance<FloatCompareCondition>();
            c.Left = _speed; c.Operator = ComparisonOperator.NotEqual; c.Right = _maxSpeed;
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        // ── StringCompare ─────────────────────────────────────────────────────

        [Test]
        public void StringCompare_Equal_SameValue()
        {
            _ctx.Set<string>(_target, "Alice");
            var c = ScriptableObject.CreateInstance<StringCompareCondition>();
            c.Left = _name; c.ExpectEqual = true; c.Right = _target;
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void StringCompare_NotEqual_DifferentValues()
        {
            var c = ScriptableObject.CreateInstance<StringCompareCondition>();
            c.Left = _name; c.ExpectEqual = false; c.Right = _target;
            try { Assert.IsTrue(c.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void StringCompare_AbsentKeys_BothEmpty_Equal()
        {
            var x = P(ParameterName.String("x"));
            var y = P(ParameterName.String("y"));
            var c = ScriptableObject.CreateInstance<StringCompareCondition>();
            c.Left = x; c.ExpectEqual = true; c.Right = y;
            try { Assert.IsTrue(c.Evaluate(new BaseContext())); }
            finally { Object.DestroyImmediate(c); }
        }
    }
}
