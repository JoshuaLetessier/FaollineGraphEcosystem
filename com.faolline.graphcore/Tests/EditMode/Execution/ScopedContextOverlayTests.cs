using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Unit tests for the BaseContext local-context overlay (0.3.0): the routing table, isolation,
    /// fall-through reads, seeding, persistence exclusion, notifications, and misuse guards.
    /// </summary>
    public class ScopedContextOverlayTests
    {
        private readonly List<BaseGraph> _graphs = new List<BaseGraph>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) UnityEngine.Object.DestroyImmediate(g);
            _graphs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }

        // ── Open / close ────────────────────────────────────────────────────────

        [Test]
        public void HasLocalContext_IsFalse_ByDefault()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(ctx.HasLocalContext);
        }

        [Test]
        public void BeginLocalContext_OpensLocal_EndCloses()
        {
            var ctx = new BaseContext();
            ctx.BeginLocalContext();
            Assert.IsTrue(ctx.HasLocalContext);
            ctx.EndLocalContext();
            Assert.IsFalse(ctx.HasLocalContext);
        }

        // ── Reads ────────────────────────────────────────────────────────────────

        [Test]
        public void Read_FallsThrough_ToGlobal_WhileScoped()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 7);
            ctx.BeginLocalContext();
            Assert.AreEqual(7, ctx.Get<int>("Gold"));   // resolved from global by fall-through
            Assert.IsTrue(ctx.Has("Gold"));
        }

        [Test]
        public void Read_LocalShadows_Global()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 7);
            ctx.BeginLocalContext();
            // create a local shadow first (undeclared key would go local; here we force a local entry)
            // Gold exists in global, so writing it routes to global; to shadow we seed a local Gold.
            ctx.EndLocalContext();
            // Use seeding to get a genuine local shadow:
            var g = Track(ScriptableObject.CreateInstance<BaseGraph>());
            g.AddParameter(new ParameterData { Key = "Gold", Type = ParameterType.Int, DefaultValue = "99" });
            ctx.BeginLocalContext(g);
            Assert.AreEqual(99, ctx.Get<int>("Gold"));   // local shadow wins
            ctx.EndLocalContext();
            Assert.AreEqual(7, ctx.Get<int>("Gold"));    // global re-exposed
        }

        [Test]
        public void Read_MissingEverywhere_ThrowsLikeToday()
        {
            var ctx = new BaseContext();
            ctx.BeginLocalContext();
            Assert.Throws<KeyNotFoundException>(() => ctx.Get<int>("nope"));
            Assert.IsFalse(ctx.Has("nope"));
            Assert.IsFalse(ctx.TryGet<int>("nope", out _));
        }

        // ── Write routing ─────────────────────────────────────────────────────────

        [Test]
        public void Write_UndeclaredKey_GoesLocal_DiscardedOnEnd()   // US1 / FR-004
        {
            var ctx = new BaseContext();
            ctx.BeginLocalContext();
            ctx.Set<int>("Tmp", 5);
            Assert.AreEqual(5, ctx.Get<int>("Tmp"));
            ctx.EndLocalContext();
            Assert.IsFalse(ctx.Has("Tmp"), "Undeclared scratch must be discarded with the local context.");
        }

        [Test]
        public void Write_GlobalResidentKey_PersistsAfterEnd()       // US2.2 / FR-006
        {
            var ctx = new BaseContext();
            ctx.Set<bool>("BossDefeated", false);
            ctx.BeginLocalContext();
            ctx.Set<bool>("BossDefeated", true);   // key lives in global → durable global write
            ctx.EndLocalContext();
            Assert.IsTrue(ctx.Get<bool>("BossDefeated"));
        }

        [Test]
        public void Write_LocalShadow_DoesNotAffectGlobal()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 7);
            var g = Track(ScriptableObject.CreateInstance<BaseGraph>());
            g.AddParameter(new ParameterData { Key = "Gold", Type = ParameterType.Int, DefaultValue = "1" });
            ctx.BeginLocalContext(g);     // local shadow Gold=1
            ctx.Set<int>("Gold", 50);     // key in local → writes the local shadow
            Assert.AreEqual(50, ctx.Get<int>("Gold"));
            ctx.EndLocalContext();
            Assert.AreEqual(7, ctx.Get<int>("Gold"), "Local shadow write must not touch global.");
        }

        // ── Seeding ────────────────────────────────────────────────────────────────

        [Test]
        public void BeginLocalContext_Seed_SeedsLocalFromGraphParameters()
        {
            var ctx = new BaseContext();
            var g = Track(ScriptableObject.CreateInstance<BaseGraph>());
            g.AddParameter(new ParameterData { Key = "Step", Type = ParameterType.Int,    DefaultValue = "3" });
            g.AddParameter(new ParameterData { Key = "Flag", Type = ParameterType.Bool,   DefaultValue = "true" });

            ctx.BeginLocalContext(g);

            Assert.AreEqual(3, ctx.Get<int>("Step"));
            Assert.IsTrue(ctx.Get<bool>("Flag"));
            ctx.EndLocalContext();
            Assert.IsFalse(ctx.Has("Step"), "Seeded local values vanish when the scope ends.");
        }

        // ── Persistence exclusion (inv.11) ──────────────────────────────────────────

        [Test]
        public void GetAllParameters_ReturnsGlobalOnly_WhileScoped()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 7);     // global
            ctx.BeginLocalContext();
            ctx.Set<int>("Tmp", 9);      // local scratch

            var all = ctx.GetAllParameters();
            Assert.IsTrue(all.ContainsKey("Gold"));
            Assert.IsFalse(all.ContainsKey("Tmp"), "Persistence snapshot must exclude local scratch.");
        }

        // ── Notifications (FR-009) ──────────────────────────────────────────────────

        [Test]
        public void Notifications_Fire_ForLocalAndGlobalWrites()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 0);     // global-resident
            int globalHits = 0, localHits = 0;
            ctx.OnParameterChanged("Gold", _ => globalHits++);
            ctx.OnParameterChanged("Tmp",  _ => localHits++);

            ctx.BeginLocalContext();
            ctx.Set<int>("Gold", 1);     // routes global
            ctx.Set<int>("Tmp", 2);      // routes local
            ctx.EndLocalContext();

            Assert.AreEqual(1, globalHits);
            Assert.AreEqual(1, localHits);
        }

        // ── Misuse guards ───────────────────────────────────────────────────────────

        [Test]
        public void BeginLocalContext_WhileOpen_DiscardsAndWarns()   // FR-011
        {
            var ctx = new BaseContext();
            ctx.BeginLocalContext();
            ctx.Set<int>("Tmp", 1);

            LogAssert.Expect(LogType.Warning,
                "[GraphCore] BeginLocalContext called while a local context is already open; " +
                "discarding the existing one (nested local contexts are not supported).");
            ctx.BeginLocalContext();     // discards the first local

            Assert.IsTrue(ctx.HasLocalContext);
            Assert.IsFalse(ctx.Has("Tmp"), "Replacing the local context discards the previous one.");
        }

        [Test]
        public void EndLocalContext_WhenNoneOpen_IsNoOpWithWarning()
        {
            var ctx = new BaseContext();
            LogAssert.Expect(LogType.Warning,
                "[GraphCore] EndLocalContext called with no local context open; ignored.");
            Assert.DoesNotThrow(() => ctx.EndLocalContext());
            Assert.IsFalse(ctx.HasLocalContext);
        }
    }
}
