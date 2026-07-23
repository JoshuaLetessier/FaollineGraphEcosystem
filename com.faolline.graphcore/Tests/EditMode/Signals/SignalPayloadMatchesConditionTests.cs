using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class SignalPayloadMatchesConditionTests
    {
        private static SignalDef Sig(string name) => SignalDef.Create(name);

        private static SignalPayloadMatchesCondition Make(SignalDef signal, string expected, SignalPayloadMatchMode mode = SignalPayloadMatchMode.Exact)
        {
            var cond = ScriptableObject.CreateInstance<SignalPayloadMatchesCondition>();
            cond.Signal = signal;
            cond.ExpectedValue = expected;
            cond.MatchMode = mode;
            return cond;
        }

        [Test]
        public void FalseBeforeAnyRaise()
        {
            var sig = Sig("loadCompleted");
            var cond = Make(sig, "ZoneSud");
            var ctx = new BaseContext();

            Assert.IsFalse(cond.Evaluate(ctx));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void TrueWhenLastPayloadMatches()
        {
            var sig = Sig("loadCompleted");
            var cond = Make(sig, "ZoneSud");
            var ctx = new BaseContext();

            ctx.RaiseSignal(sig, "ZoneSud");

            Assert.IsTrue(cond.Evaluate(ctx));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void FalseWhenLastPayloadDiffers()
        {
            var sig = Sig("loadCompleted");
            var cond = Make(sig, "ZoneSud");
            var ctx = new BaseContext();

            // A homonymous raise for a different tile/zone must NOT match.
            ctx.RaiseSignal(sig, "ZoneNord");

            Assert.IsFalse(cond.Evaluate(ctx));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void SharedSignalCrossTalk_OnlyTheMatchingTileResumes()
        {
            // The concrete proximity-streaming repro from TODO.md's "Signal scoping" entry: one shared
            // LoadCompletedSignal, two tiles independently parked on it. Without this condition both would
            // resume on either tile's completion; with it, only the matching one does.
            var loadCompleted = Sig("loadCompleted");
            var ctx = new BaseContext();

            var zoneNordCond = Make(loadCompleted, "ZoneNord");
            var zoneSudCond  = Make(loadCompleted, "ZoneSud");

            ctx.RaiseSignal(loadCompleted, "ZoneNord");

            Assert.IsTrue(zoneNordCond.Evaluate(ctx),  "ZoneNord's own load should resolve its wait.");
            Assert.IsFalse(zoneSudCond.Evaluate(ctx),  "ZoneSud must stay parked on ZoneNord's completion.");

            ctx.RaiseSignal(loadCompleted, "ZoneSud");

            Assert.IsTrue(zoneSudCond.Evaluate(ctx),   "ZoneSud's own load should now resolve its wait.");

            Object.DestroyImmediate(zoneNordCond);
            Object.DestroyImmediate(zoneSudCond);
            Object.DestroyImmediate(loadCompleted);
        }

        [Test]
        public void NonStringPayload_NeverMatches()
        {
            var sig = Sig("loadCompleted");
            var cond = Make(sig, "ZoneSud");
            var ctx = new BaseContext();

            ctx.RaiseSignal<int>(sig, 42);

            Assert.IsFalse(cond.Evaluate(ctx));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void NoPayload_NeverMatches()
        {
            var sig = Sig("loadCompleted");
            var cond = Make(sig, "ZoneSud");
            var ctx = new BaseContext();

            ctx.RaiseSignal(sig);

            Assert.IsFalse(cond.Evaluate(ctx));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void NullSignal_ReturnsFalse()
        {
            var cond = Make(null, "ZoneSud");
            var ctx = new BaseContext();

            Assert.IsFalse(cond.Evaluate(ctx));

            Object.DestroyImmediate(cond);
        }

        [Test]
        public void NullContext_ReturnsFalse()
        {
            var sig = Sig("loadCompleted");
            var cond = Make(sig, "ZoneSud");

            Assert.IsFalse(cond.Evaluate(null));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        // ── IResumeSignalAwareCondition.EvaluateResume ──────────────────────────

        [Test]
        public void EvaluateResume_DifferentRaisedName_Abstains_ReturnsTrue()
        {
            var sig   = Sig("loadCompleted");
            var other = Sig("loadFailed");
            var cond  = Make(sig, "ZoneSud");
            var ctx   = new BaseContext();

            // Nothing about "loadFailed" was even raised — this condition simply isn't the one to judge it.
            Assert.IsTrue(((IResumeSignalAwareCondition)cond).EvaluateResume(ctx, (string)other));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
            Object.DestroyImmediate(other);
        }

        [Test]
        public void EvaluateResume_OwnRaisedName_AppliesMatchRule()
        {
            var sig  = Sig("loadCompleted");
            var cond = Make(sig, "ZoneSud");
            var ctx  = new BaseContext();
            ctx.RaiseSignal(sig, "ZoneNord");

            Assert.IsFalse(((IResumeSignalAwareCondition)cond).EvaluateResume(ctx, (string)sig));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void EvaluateResume_NullRaisedName_BehavesLikeEvaluate()
        {
            var sig  = Sig("loadCompleted");
            var cond = Make(sig, "ZoneSud");
            var ctx  = new BaseContext();
            ctx.RaiseSignal(sig, "ZoneSud");

            Assert.IsTrue(((IResumeSignalAwareCondition)cond).EvaluateResume(ctx, null));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        // ── StartsWith mode ──────────────────────────────────────────────────

        [Test]
        public void StartsWithMode_MatchesPrefixedPayload()
        {
            var sig  = Sig("loadFailed");
            var cond = Make(sig, "ZoneSud", SignalPayloadMatchMode.StartsWith);
            var ctx  = new BaseContext();

            ctx.RaiseSignal(sig, "ZoneSud: Scene 'ZoneSud' is not loaded; unload ignored.");

            Assert.IsTrue(cond.Evaluate(ctx));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void StartsWithMode_RejectsNonPrefixedPayload()
        {
            var sig  = Sig("loadFailed");
            var cond = Make(sig, "ZoneSud", SignalPayloadMatchMode.StartsWith);
            var ctx  = new BaseContext();

            ctx.RaiseSignal(sig, "ZoneNord: Scene 'ZoneNord' is not loaded; unload ignored.");

            Assert.IsFalse(cond.Evaluate(ctx));

            Object.DestroyImmediate(cond);
            Object.DestroyImmediate(sig);
        }
    }
}
