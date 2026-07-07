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

        private static SignalName Sig(string n) => SignalName.Create(n);

        [Test]
        public void RaiseSignalAction_RaisesNamedSignal()
        {
            var a = ScriptableObject.CreateInstance<RaiseSignalAction>();
            var sig = Sig("door_open");
            a.Signal = sig;
            bool received = false;
            // Asset signals key on the GUID (islands) — subscribe on the asset, not a raw literal.
            _ctx.OnSignal(sig, _ => received = true);
            try
            {
                a.Execute(_ctx);
                Assert.IsTrue(received);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(sig); }
        }

        [Test]
        public void RaiseSignalAction_NullSignal_IsNoOp()
        {
            var a = ScriptableObject.CreateInstance<RaiseSignalAction>();
            try { a.Execute(_ctx); }
            finally { Object.DestroyImmediate(a); }
        }

        // ── ToggleBoolAction ──────────────────────────────────────────────────

        [Test]
        public void ToggleBool_FlipsValue()
        {
            var flag = ParameterName.Bool("flag");
            _ctx.Set<bool>(flag, false);
            var a = ScriptableObject.CreateInstance<ToggleBoolAction>();
            a.Parameter = flag;
            try
            {
                a.Execute(_ctx);
                Assert.IsTrue(_ctx.Get<bool>(flag));
                a.Execute(_ctx);
                Assert.IsFalse(_ctx.Get<bool>(flag));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(flag); }
        }

        [Test]
        public void ToggleBool_AbsentKey_SetsTrue()
        {
            var flag = ParameterName.Bool("new_flag");
            var a = ScriptableObject.CreateInstance<ToggleBoolAction>();
            a.Parameter = flag;
            try
            {
                a.Execute(_ctx);
                Assert.IsTrue(_ctx.Get<bool>(flag));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(flag); }
        }

        // ── SetRandomIntAction ────────────────────────────────────────────────

        [Test]
        public void SetRandomInt_ValueInRange()
        {
            var roll = ParameterName.Int("roll");
            var a = ScriptableObject.CreateInstance<SetRandomIntAction>();
            a.Parameter = roll;
            a.Min = 1;
            a.Max = 6;
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    a.Execute(_ctx);
                    var v = _ctx.Get<int>(roll);
                    Assert.IsTrue(v >= 1 && v <= 6, $"Roll {v} out of [1,6]");
                }
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(roll); }
        }
    }
}
