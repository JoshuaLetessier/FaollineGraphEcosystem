using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphTest;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class ActionTests
    {
        private BaseContext _context;

        [SetUp]
        public void SetUp() => _context = new BaseContext();

        // ── TestLogAction ─────────────────────────────────────────────────────

        [Test]
        public void TestLogAction_Execute_LogsMessage()
        {
            var action = ScriptableObject.CreateInstance<TestLogAction>();
            action.Message = "hello from test";
            try
            {
                LogAssert.Expect(LogType.Log, "[GraphTest] Action: hello from test");
                action.Execute(_context);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void TestLogAction_HasCreateAssetMenuAttribute()
        {
            var attrs = typeof(TestLogAction).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false);
            Assert.IsNotEmpty(attrs);
        }

        // ── TestSetBoolAction ─────────────────────────────────────────────────

        [Test]
        public void TestSetBoolAction_Execute_WritesValueToContext()
        {
            var action = ScriptableObject.CreateInstance<TestSetBoolAction>();
            action.ParameterKey = "door_open";
            action.Value = true;
            try
            {
                action.Execute(_context);
                Assert.IsTrue(_context.TryGet<bool>("door_open", out var v) && v,
                    "TestSetBoolAction must write the bool value into the context");
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void TestSetBoolAction_Execute_OverwritesPreviousValue()
        {
            _context.Set<bool>("door_open", true);
            var action = ScriptableObject.CreateInstance<TestSetBoolAction>();
            action.ParameterKey = "door_open";
            action.Value = false;
            try
            {
                action.Execute(_context);
                Assert.IsTrue(_context.TryGet<bool>("door_open", out var v));
                Assert.IsFalse(v, "TestSetBoolAction must overwrite the existing value");
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void TestSetBoolAction_HasCreateAssetMenuAttribute()
        {
            var attrs = typeof(TestSetBoolAction).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false);
            Assert.IsNotEmpty(attrs);
        }
    }
}
