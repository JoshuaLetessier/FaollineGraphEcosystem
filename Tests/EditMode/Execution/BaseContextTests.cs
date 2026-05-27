using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class ExecutionBaseContextTests
    {
        // ── Set / Get ─────────────────────────────────────────────────────────

        [Test]
        public void Set_And_Get_Bool_ReturnsCorrectValue()
        {
            var ctx = new BaseContext();
            ctx.Set<bool>("flag", true);
            Assert.AreEqual(true, ctx.Get<bool>("flag"));
        }

        [Test]
        public void Set_And_Get_Int_ReturnsCorrectValue()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 42);
            Assert.AreEqual(42, ctx.Get<int>("score"));
        }

        [Test]
        public void Set_And_Get_Float_ReturnsCorrectValue()
        {
            var ctx = new BaseContext();
            ctx.Set<float>("speed", 3.14f);
            Assert.AreEqual(3.14f, ctx.Get<float>("speed"), 0.0001f);
        }

        [Test]
        public void Set_And_Get_String_ReturnsCorrectValue()
        {
            var ctx = new BaseContext();
            ctx.Set<string>("name", "Hero");
            Assert.AreEqual("Hero", ctx.Get<string>("name"));
        }

        [Test]
        public void Set_OverwritesExistingValue()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 10);
            ctx.Set<int>("score", 99);
            Assert.AreEqual(99, ctx.Get<int>("score"));
        }

        [Test]
        public void Set_UnsupportedType_ThrowsArgumentException()
        {
            var ctx = new BaseContext();
            Assert.Throws<ArgumentException>(() => ctx.Set<double>("x", 1.0));
        }

        // ── TryGet / Has ──────────────────────────────────────────────────────

        [Test]
        public void TryGet_ExistingKey_ReturnsTrueAndValue()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 7);
            bool result = ctx.TryGet<int>("score", out int val);
            Assert.IsTrue(result);
            Assert.AreEqual(7, val);
        }

        [Test]
        public void TryGet_MissingKey_ReturnsFalseAndDefault()
        {
            var ctx = new BaseContext();
            bool result = ctx.TryGet<int>("missing", out int val);
            Assert.IsFalse(result);
            Assert.AreEqual(default(int), val);
        }

        [Test]
        public void Has_ExistingKey_ReturnsTrue()
        {
            var ctx = new BaseContext();
            ctx.Set<bool>("ready", false);
            Assert.IsTrue(ctx.Has("ready"));
        }

        [Test]
        public void Has_MissingKey_ReturnsFalse()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(ctx.Has("unknown"));
        }

        [Test]
        public void Get_MissingKey_ThrowsKeyNotFoundException()
        {
            var ctx = new BaseContext();
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => ctx.Get<int>("nope"));
        }

        // ── Change notifications ───────────────────────────────────────────────

        [Test]
        public void OnParameterChanged_FiredOnSet()
        {
            var ctx = new BaseContext();
            object received = null;
            Action<object> handler = v => received = v;
            ctx.OnParameterChanged("score", handler);

            ctx.Set<int>("score", 100);

            Assert.IsNotNull(received);
            Assert.AreEqual(100, (int)received);
        }

        [Test]
        public void OnParameterChanged_NotFiredForDifferentKey()
        {
            var ctx = new BaseContext();
            bool fired = false;
            ctx.OnParameterChanged("score", _ => fired = true);

            ctx.Set<int>("other", 5);

            Assert.IsFalse(fired);
        }

        [Test]
        public void OffParameterChanged_NotFiredAfterUnsubscribe()
        {
            var ctx = new BaseContext();
            int callCount = 0;
            Action<object> handler = _ => callCount++;
            ctx.OnParameterChanged("score", handler);
            ctx.Set<int>("score", 1);

            ctx.OffParameterChanged("score", handler);
            ctx.Set<int>("score", 2);

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void OnParameterChanged_NotFiredForInitialSetIfKeyAbsent()
        {
            // The event fires whenever Set is called, even first time
            var ctx = new BaseContext();
            int calls = 0;
            ctx.OnParameterChanged("hp", _ => calls++);
            ctx.Set<int>("hp", 50);
            Assert.AreEqual(1, calls);
        }

        // ── DeepClone ─────────────────────────────────────────────────────────

        [Test]
        public void DeepClone_CopiesValues()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 10);
            ctx.Set<bool>("done", true);

            var clone = ctx.DeepClone();

            Assert.AreEqual(10, clone.Get<int>("score"));
            Assert.AreEqual(true, clone.Get<bool>("done"));
        }

        [Test]
        public void DeepClone_DoesNotShareValues()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 10);

            var clone = ctx.DeepClone();
            ctx.Set<int>("score", 99);

            Assert.AreEqual(10, clone.Get<int>("score"),
                "Clone should not be affected by changes to original.");
        }

        [Test]
        public void DeepClone_DoesNotCopySubscriptions()
        {
            var ctx = new BaseContext();
            int calls = 0;
            ctx.OnParameterChanged("score", _ => calls++);

            var clone = ctx.DeepClone();
            clone.Set<int>("score", 5);

            Assert.AreEqual(0, calls, "Clone subscribers must be empty.");
        }

        [Test]
        public void DeepClone_EmptyContext_ReturnsValidContext()
        {
            var ctx = new BaseContext();
            var clone = ctx.DeepClone();
            Assert.IsNotNull(clone);
            Assert.IsFalse(clone.Has("anything"));
        }

        // ── InitFromGraph ─────────────────────────────────────────────────────

        [Test]
        public void InitFromGraph_PopulatesBoolParameter()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            graph.AddParameter(new ParameterData { Key = "IsReady", Type = ParameterType.Bool, DefaultValue = "true" });

            var ctx = new BaseContext();
            ctx.InitFromGraph(graph);

            Assert.AreEqual(true, ctx.Get<bool>("IsReady"));
            UnityEngine.Object.DestroyImmediate(graph);
        }

        [Test]
        public void InitFromGraph_PopulatesIntParameter()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            graph.AddParameter(new ParameterData { Key = "Score", Type = ParameterType.Int, DefaultValue = "99" });

            var ctx = new BaseContext();
            ctx.InitFromGraph(graph);

            Assert.AreEqual(99, ctx.Get<int>("Score"));
            UnityEngine.Object.DestroyImmediate(graph);
        }

        [Test]
        public void InitFromGraph_PopulatesFloatParameter()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            graph.AddParameter(new ParameterData { Key = "Speed", Type = ParameterType.Float, DefaultValue = "1.5" });

            var ctx = new BaseContext();
            ctx.InitFromGraph(graph);

            Assert.AreEqual(1.5f, ctx.Get<float>("Speed"), 0.0001f);
            UnityEngine.Object.DestroyImmediate(graph);
        }

        [Test]
        public void InitFromGraph_PopulatesStringParameter()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            graph.AddParameter(new ParameterData { Key = "Name", Type = ParameterType.String, DefaultValue = "Hero" });

            var ctx = new BaseContext();
            ctx.InitFromGraph(graph);

            Assert.AreEqual("Hero", ctx.Get<string>("Name"));
            UnityEngine.Object.DestroyImmediate(graph);
        }

        [Test]
        public void InitFromGraph_InvalidBoolValue_UsesDefault()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            graph.AddParameter(new ParameterData { Key = "Flag", Type = ParameterType.Bool, DefaultValue = "notabool" });

            var ctx = new BaseContext();
            ctx.InitFromGraph(graph);

            Assert.AreEqual(default(bool), ctx.Get<bool>("Flag"));
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }
}
