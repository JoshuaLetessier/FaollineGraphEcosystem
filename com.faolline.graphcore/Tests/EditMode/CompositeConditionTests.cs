using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class CompositeConditionTests
    {
        private BaseContext _ctx;
        private VariableDef _a, _b;

        [SetUp]
        public void SetUp()
        {
            _ctx = new BaseContext();
            _a = VariableDef.Bool("a");
            _b = VariableDef.Bool("b");
            _ctx.Set<bool>(_a, true);
            _ctx.Set<bool>(_b, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
        }

        private BoolCondition Bool(VariableDef param, bool expected)
        {
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.Variable = param;
            c.ExpectedValue = expected;
            return c;
        }

        // ── And ───────────────────────────────────────────────────────────────

        [Test]
        public void And_AllTrue_ReturnsTrue()
        {
            var and = ScriptableObject.CreateInstance<AndCondition>();
            var c1 = Bool(_a, true);
            var c2 = ScriptableObject.CreateInstance<AlwaysTrueCondition>();
            and.Conditions.Add(c1);
            and.Conditions.Add(c2);
            try { Assert.IsTrue(and.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(and); Object.DestroyImmediate(c1); Object.DestroyImmediate(c2); }
        }

        [Test]
        public void And_OneFalse_ReturnsFalse()
        {
            var and = ScriptableObject.CreateInstance<AndCondition>();
            var c1 = Bool(_a, true);
            var c2 = Bool(_b, true);
            and.Conditions.Add(c1);
            and.Conditions.Add(c2);
            try { Assert.IsFalse(and.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(and); Object.DestroyImmediate(c1); Object.DestroyImmediate(c2); }
        }

        [Test]
        public void And_Empty_ReturnsTrue()
        {
            var and = ScriptableObject.CreateInstance<AndCondition>();
            try { Assert.IsTrue(and.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(and); }
        }

        // ── Or ────────────────────────────────────────────────────────────────

        [Test]
        public void Or_OneTrue_ReturnsTrue()
        {
            var or = ScriptableObject.CreateInstance<OrCondition>();
            var c1 = Bool(_b, true);
            var c2 = Bool(_a, true);
            or.Conditions.Add(c1);
            or.Conditions.Add(c2);
            try { Assert.IsTrue(or.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(or); Object.DestroyImmediate(c1); Object.DestroyImmediate(c2); }
        }

        [Test]
        public void Or_AllFalse_ReturnsFalse()
        {
            var or = ScriptableObject.CreateInstance<OrCondition>();
            var c1 = Bool(_a, false);
            var c2 = Bool(_b, true);
            or.Conditions.Add(c1);
            or.Conditions.Add(c2);
            try { Assert.IsFalse(or.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(or); Object.DestroyImmediate(c1); Object.DestroyImmediate(c2); }
        }

        [Test]
        public void Or_Empty_ReturnsFalse()
        {
            var or = ScriptableObject.CreateInstance<OrCondition>();
            try { Assert.IsFalse(or.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(or); }
        }

        // ── Not ───────────────────────────────────────────────────────────────

        [Test]
        public void Not_NegatesInner()
        {
            var not = ScriptableObject.CreateInstance<NotCondition>();
            var inner = Bool(_a, true);
            not.Condition = inner;
            try
            {
                Assert.IsFalse(not.Evaluate(_ctx));
                inner.ExpectedValue = false;
                Assert.IsTrue(not.Evaluate(_ctx));
            }
            finally { Object.DestroyImmediate(not); Object.DestroyImmediate(inner); }
        }

        [Test]
        public void Not_NullInner_ReturnsTrue()
        {
            var not = ScriptableObject.CreateInstance<NotCondition>();
            try { Assert.IsTrue(not.Evaluate(_ctx)); }
            finally { Object.DestroyImmediate(not); }
        }

        // ── Nesting ───────────────────────────────────────────────────────────

        [Test]
        public void Nested_OrInsideAnd()
        {
            var or = ScriptableObject.CreateInstance<OrCondition>();
            or.Conditions.Add(Bool(_b, true));
            or.Conditions.Add(Bool(_a, true));

            var and = ScriptableObject.CreateInstance<AndCondition>();
            and.Conditions.Add(or);
            and.Conditions.Add(Bool(_a, true));

            try { Assert.IsTrue(and.Evaluate(_ctx)); }
            finally
            {
                foreach (var c in or.Conditions) Object.DestroyImmediate(c);
                foreach (var c in and.Conditions) if (c != or) Object.DestroyImmediate(c);
                Object.DestroyImmediate(or);
                Object.DestroyImmediate(and);
            }
        }
    }
}
