using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class SignalRaisedTests
    {
        // Asset signals key on their GUID (islands); tests that pair an asset with a raise/check pass the
        // asset itself (implicit → GUID), not a raw literal.
        private static SignalDef Sig(string name) => SignalDef.Create(name);

        // ── HasSignalBeenRaised ────────────────────────────────────────────────

        [Test]
        public void HasSignalBeenRaised_FalseBeforeRaise()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(ctx.HasSignalBeenRaised("door_open"));
        }

        [Test]
        public void HasSignalBeenRaised_TrueAfterRaise()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal("door_open");
            Assert.IsTrue(ctx.HasSignalBeenRaised("door_open"));
        }

        [Test]
        public void HasSignalBeenRaised_DoesNotCrossContaminate()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal("door_open");
            Assert.IsFalse(ctx.HasSignalBeenRaised("chest_open"));
        }

        [Test]
        public void HasSignalBeenRaised_NullOrEmpty_ReturnsFalse()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(ctx.HasSignalBeenRaised(null));
            Assert.IsFalse(ctx.HasSignalBeenRaised(string.Empty));
        }

        // ── ForgetSignal ───────────────────────────────────────────────────────

        [Test]
        public void ForgetSignal_ClearsHistory()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal("door_open");
            ctx.ForgetSignal("door_open");
            Assert.IsFalse(ctx.HasSignalBeenRaised("door_open"));
        }

        [Test]
        public void ForgetSignal_AbsentName_IsNoOp()
        {
            var ctx = new BaseContext();
            Assert.DoesNotThrow(() => ctx.ForgetSignal("never_raised"));
        }

        // ── GetAllRaisedSignals / RestoreSignalHistory ─────────────────────────

        [Test]
        public void GetAllRaisedSignals_ReturnsSnapshot()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal("a");
            ctx.RaiseSignal("b");
            var all = new System.Collections.Generic.HashSet<string>(ctx.GetAllRaisedSignals());
            Assert.IsTrue(all.Contains("a"));
            Assert.IsTrue(all.Contains("b"));
            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void DeepClone_CopiesSignalHistory()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal("door_open");
            var clone = ctx.DeepClone();
            Assert.IsTrue(clone.HasSignalBeenRaised("door_open"));
        }

        [Test]
        public void DeepClone_HistoryIsIndependent()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal("door_open");
            var clone = ctx.DeepClone();
            ctx.ForgetSignal("door_open");
            Assert.IsTrue(clone.HasSignalBeenRaised("door_open"),
                "Forgetting in source should not affect clone.");
        }

        // ── SignalRaisedCondition ──────────────────────────────────────────────

        [Test]
        public void SignalRaisedCondition_FalseBeforeRaise()
        {
            var sig = Sig("npc_met");
            var cond = ScriptableObject.CreateInstance<SignalRaisedCondition>();
            cond.Signal = sig;
            var ctx = new BaseContext();
            Assert.IsFalse(cond.Evaluate(ctx));
            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void SignalRaisedCondition_TrueAfterRaise()
        {
            var sig = Sig("npc_met");
            var cond = ScriptableObject.CreateInstance<SignalRaisedCondition>();
            cond.Signal = sig;
            var ctx = new BaseContext();
            ctx.RaiseSignal(sig);
            Assert.IsTrue(cond.Evaluate(ctx));
            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void SignalRaisedCondition_NullSignal_ReturnsFalse()
        {
            var cond = ScriptableObject.CreateInstance<SignalRaisedCondition>();
            cond.Signal = null;
            var ctx = new BaseContext();
            Assert.IsFalse(cond.Evaluate(ctx));
            Object.DestroyImmediate(cond);
        }

        [Test]
        public void SignalRaisedCondition_NullContext_ReturnsFalse()
        {
            var sig = Sig("npc_met");
            var cond = ScriptableObject.CreateInstance<SignalRaisedCondition>();
            cond.Signal = sig;
            Assert.IsFalse(cond.Evaluate(null));
            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        // ── ForgetSignalAction ─────────────────────────────────────────────────

        [Test]
        public void ForgetSignalAction_ClearsHistory()
        {
            var sig = Sig("npc_met");
            var action = ScriptableObject.CreateInstance<ForgetSignalAction>();
            action.Signal = sig;
            var ctx = new BaseContext();
            ctx.RaiseSignal(sig);
            action.Execute(ctx);
            Assert.IsFalse(ctx.HasSignalBeenRaised(sig));
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void ForgetSignalAction_NullSignal_IsNoOp()
        {
            var action = ScriptableObject.CreateInstance<ForgetSignalAction>();
            action.Signal = null;
            Assert.DoesNotThrow(() => action.Execute(new BaseContext()));
            Object.DestroyImmediate(action);
        }

        // ── DicePlayer pattern (the motivating use-case) ───────────────────────

        [Test]
        public void DicePlayerPattern_ChoiceRoutesCorrectlyOnFirstAndSecondVisit()
        {
            var npcMet = Sig("dice_player_met");

            var firstTimeCond = ScriptableObject.CreateInstance<NotCondition>();
            var innerCond = ScriptableObject.CreateInstance<SignalRaisedCondition>();
            innerCond.Signal = npcMet;
            firstTimeCond.Condition = innerCond;

            var secondTimeCond = ScriptableObject.CreateInstance<SignalRaisedCondition>();
            secondTimeCond.Signal = npcMet;

            var ctx = new BaseContext();

            // First visit: signal not yet raised.
            Assert.IsTrue(firstTimeCond.Evaluate(ctx),  "First time: should take first-interaction branch");
            Assert.IsFalse(secondTimeCond.Evaluate(ctx), "First time: should NOT take replay branch");

            // Dialogue ends — raise the signal (RaiseSignalAction fires on exit).
            ctx.RaiseSignal(npcMet);

            // Second visit: signal is in history.
            Assert.IsFalse(firstTimeCond.Evaluate(ctx), "Second time: should NOT take first-interaction branch");
            Assert.IsTrue(secondTimeCond.Evaluate(ctx),  "Second time: should take replay branch");

            Object.DestroyImmediate(npcMet);
            Object.DestroyImmediate(firstTimeCond);
            Object.DestroyImmediate(innerCond);
            Object.DestroyImmediate(secondTimeCond);
        }
    }
}
