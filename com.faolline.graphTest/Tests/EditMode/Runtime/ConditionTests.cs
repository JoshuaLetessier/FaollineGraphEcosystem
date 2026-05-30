using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphTest;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class ConditionTests
    {
        private BaseContext _context;

        [SetUp]
        public void SetUp() => _context = new BaseContext();

        // ── TestAlwaysTrueCondition ───────────────────────────────────────────

        [Test]
        public void TestAlwaysTrueCondition_AlwaysReturnsTrue()
        {
            var cond = ScriptableObject.CreateInstance<TestAlwaysTrueCondition>();
            try { Assert.IsTrue(cond.Evaluate(_context)); }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void TestAlwaysTrueCondition_HasCreateAssetMenuAttribute()
        {
            var attrs = typeof(TestAlwaysTrueCondition).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false);
            Assert.IsNotEmpty(attrs);
        }

        // ── TestAlwaysFalseCondition ──────────────────────────────────────────

        [Test]
        public void TestAlwaysFalseCondition_AlwaysReturnsFalse()
        {
            var cond = ScriptableObject.CreateInstance<TestAlwaysFalseCondition>();
            try { Assert.IsFalse(cond.Evaluate(_context)); }
            finally { Object.DestroyImmediate(cond); }
        }

        // ── TestBoolCondition ─────────────────────────────────────────────────

        [Test]
        public void TestBoolCondition_KeyTrue_ExpectedTrue_ReturnsTrue()
        {
            _context.Set<bool>("door_open", true);
            var cond = ScriptableObject.CreateInstance<TestBoolCondition>();
            cond.ParameterKey = "door_open";
            cond.ExpectedValue = true;
            try { Assert.IsTrue(cond.Evaluate(_context)); }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void TestBoolCondition_KeyFalse_ExpectedTrue_ReturnsFalse()
        {
            _context.Set<bool>("door_open", false);
            var cond = ScriptableObject.CreateInstance<TestBoolCondition>();
            cond.ParameterKey = "door_open";
            cond.ExpectedValue = true;
            try { Assert.IsFalse(cond.Evaluate(_context)); }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void TestBoolCondition_KeyFalse_ExpectedFalse_ReturnsTrue()
        {
            _context.Set<bool>("door_open", false);
            var cond = ScriptableObject.CreateInstance<TestBoolCondition>();
            cond.ParameterKey = "door_open";
            cond.ExpectedValue = false;
            try { Assert.IsTrue(cond.Evaluate(_context)); }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void TestBoolCondition_MissingKey_ReturnsFalseWithWarning()
        {
            var cond = ScriptableObject.CreateInstance<TestBoolCondition>();
            cond.ParameterKey = "missing_key";
            cond.ExpectedValue = true;
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"missing_key"));
                Assert.IsFalse(cond.Evaluate(_context));
            }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void TestBoolCondition_HasCreateAssetMenuAttribute()
        {
            var attrs = typeof(TestBoolCondition).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false);
            Assert.IsNotEmpty(attrs);
        }
    }
}
