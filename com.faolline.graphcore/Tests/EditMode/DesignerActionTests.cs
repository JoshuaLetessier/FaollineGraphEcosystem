using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class DesignerActionTests
    {
        private BaseContext _ctx;

        [SetUp]
        public void SetUp() => _ctx = new BaseContext();

        // ── RaiseSignalAction ─────────────────────────────────────────────────

        [Test]
        public void RaiseSignalAction_RaisesViaRawString()
        {
            var a = ScriptableObject.CreateInstance<RaiseSignalAction>();
            a.SignalRaw = "door_open";
            bool received = false;
            _ctx.OnSignal("door_open", _ => received = true);
            try
            {
                a.Execute(_ctx);
                Assert.IsTrue(received);
            }
            finally { Object.DestroyImmediate(a); }
        }

        [Test]
        public void RaiseSignalAction_RaisesViaSignalAsset()
        {
            var a = ScriptableObject.CreateInstance<RaiseSignalAction>();
            var sig = ScriptableObject.CreateInstance<SignalName>();
            sig.name = "unlock";
            a.SignalAsset = sig;
            bool received = false;
            _ctx.OnSignal("unlock", _ => received = true);
            try
            {
                a.Execute(_ctx);
                Assert.IsTrue(received);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(sig); }
        }

        [Test]
        public void RaiseSignalAction_AssetTakesPrecedenceOverRaw()
        {
            var a = ScriptableObject.CreateInstance<RaiseSignalAction>();
            var sig = ScriptableObject.CreateInstance<SignalName>();
            sig.name = "asset_signal";
            a.SignalAsset = sig;
            a.SignalRaw = "raw_signal";
            string raised = null;
            _ctx.OnSignal("asset_signal", args => raised = args.Name);
            _ctx.OnSignal("raw_signal", args => raised = args.Name);
            try
            {
                a.Execute(_ctx);
                Assert.AreEqual("asset_signal", raised);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(sig); }
        }

        [Test]
        public void RaiseSignalAction_EmptyBoth_IsNoOp()
        {
            var a = ScriptableObject.CreateInstance<RaiseSignalAction>();
            a.SignalRaw = "";
            try { a.Execute(_ctx); }
            finally { Object.DestroyImmediate(a); }
        }

        // ── ToggleBoolAction ──────────────────────────────────────────────────

        [Test]
        public void ToggleBool_FlipsValue()
        {
            _ctx.Set<bool>("flag", false);
            var a = ScriptableObject.CreateInstance<ToggleBoolAction>();
            a.ParameterKey = "flag";
            try
            {
                a.Execute(_ctx);
                Assert.IsTrue(_ctx.Get<bool>("flag"));
                a.Execute(_ctx);
                Assert.IsFalse(_ctx.Get<bool>("flag"));
            }
            finally { Object.DestroyImmediate(a); }
        }

        [Test]
        public void ToggleBool_AbsentKey_SetsTrue()
        {
            var a = ScriptableObject.CreateInstance<ToggleBoolAction>();
            a.ParameterKey = "new_flag";
            try
            {
                a.Execute(_ctx);
                Assert.IsTrue(_ctx.Get<bool>("new_flag"));
            }
            finally { Object.DestroyImmediate(a); }
        }

        // ── SetRandomIntAction ────────────────────────────────────────────────

        [Test]
        public void SetRandomInt_ValueInRange()
        {
            var a = ScriptableObject.CreateInstance<SetRandomIntAction>();
            a.ParameterKey = "roll";
            a.Min = 1;
            a.Max = 6;
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    a.Execute(_ctx);
                    var v = _ctx.Get<int>("roll");
                    Assert.IsTrue(v >= 1 && v <= 6, $"Roll {v} out of [1,6]");
                }
            }
            finally { Object.DestroyImmediate(a); }
        }
    }
}
