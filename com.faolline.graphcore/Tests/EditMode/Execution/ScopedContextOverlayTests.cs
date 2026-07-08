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
        private readonly List<Object> _objs = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) UnityEngine.Object.DestroyImmediate(g);
            _graphs.Clear();
            foreach (var o in _objs) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _objs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }
        private T TrackObj<T>(T o) where T : Object { _objs.Add(o); return o; }

        // Builds a graph that references each VariableDef (via an inert ResumeConditions probe on one node),
        // so BeginLocalContext(graph)/InitFromGraph discovers them and seeds their asset defaults.
        private BaseGraph GraphReferencing(params VariableDef[] parameters)
        {
            var g = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var node = new StatementNodeData { Id = "n", NodeType = StatementNodeData.NodeTypeId };
            foreach (var p in parameters)
            {
                var probe = TrackObj(ScriptableObject.CreateInstance<IntCondition>()); // type irrelevant to seeding
                probe.Variable = p;
                node.ResumeConditions.Add(probe);
            }
            g.AddNode(node);
            return g;
        }

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
            var gold = TrackObj(VariableDef.Int("Gold", 99));
            ctx.Set<int>(gold, 7);
            // Seed a genuine local shadow from a graph that references Gold (default 99):
            var g = GraphReferencing(gold);
            ctx.BeginLocalContext(g);
            Assert.AreEqual(99, ctx.Get<int>(gold));   // local shadow wins
            ctx.EndLocalContext();
            Assert.AreEqual(7, ctx.Get<int>(gold));    // global re-exposed
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
            var gold = TrackObj(VariableDef.Int("Gold", 1));
            ctx.Set<int>(gold, 7);
            var g = GraphReferencing(gold);
            ctx.BeginLocalContext(g);     // local shadow Gold=1
            ctx.Set<int>(gold, 50);       // key in local → writes the local shadow
            Assert.AreEqual(50, ctx.Get<int>(gold));
            ctx.EndLocalContext();
            Assert.AreEqual(7, ctx.Get<int>(gold), "Local shadow write must not touch global.");
        }

        // ── Seeding ────────────────────────────────────────────────────────────────

        [Test]
        public void BeginLocalContext_Seed_SeedsLocalFromGraphVariables()
        {
            var ctx = new BaseContext();
            var step = TrackObj(VariableDef.Int("Step", 3));
            var flag = TrackObj(VariableDef.Bool("Flag", true));
            var g = GraphReferencing(step, flag);

            ctx.BeginLocalContext(g);

            Assert.AreEqual(3, ctx.Get<int>(step));
            Assert.IsTrue(ctx.Get<bool>(flag));
            ctx.EndLocalContext();
            Assert.IsFalse(ctx.Has(step), "Seeded local values vanish when the scope ends.");
        }

        // ── Persistence exclusion (inv.11) ──────────────────────────────────────────

        [Test]
        public void GetAllVariables_ReturnsGlobalOnly_WhileScoped()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 7);     // global
            ctx.BeginLocalContext();
            ctx.Set<int>("Tmp", 9);      // local scratch

            var all = ctx.GetAllVariables();
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
            ctx.OnVariableChanged("Gold", _ => globalHits++);
            ctx.OnVariableChanged("Tmp",  _ => localHits++);

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
