using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseContextTests
    {
        // No shared mutable state — every test constructs its own BaseContext locally.

        // ── Set / Get ─────────────────────────────────────────────────────────

        [Test]
        public void Set_Get_Bool_ReturnsCorrectValue()
        {
            var ctx = new BaseContext();
            ctx.Set<bool>("flag", true);

            Assert.AreEqual(true, ctx.Get<bool>("flag"));
        }

        [Test]
        public void Set_Get_Int_ReturnsCorrectValue()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 42);

            Assert.AreEqual(42, ctx.Get<int>("score"));
        }

        [Test]
        public void Set_Get_Float_ReturnsCorrectValue()
        {
            var ctx = new BaseContext();
            ctx.Set<float>("speed", 3.14f);

            Assert.AreEqual(3.14f, ctx.Get<float>("speed"), 0.0001f);
        }

        [Test]
        public void Set_Get_String_ReturnsCorrectValue()
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

            Assert.Throws<KeyNotFoundException>(() => ctx.Get<int>("nope"));
        }

        // ── OnVariableChanged ────────────────────────────────────────────────

        [Test]
        public void OnParameterChanged_FiredOnSet()
        {
            var ctx = new BaseContext();
            object received = null;
            ctx.OnVariableChanged("score", v => received = v);

            ctx.Set<int>("score", 100);

            Assert.AreEqual(100, (int)received);
        }

        [Test]
        public void OnParameterChanged_NotFiredForDifferentKey()
        {
            var ctx = new BaseContext();
            bool fired = false;
            ctx.OnVariableChanged("score", _ => fired = true);

            ctx.Set<int>("other", 5);

            Assert.IsFalse(fired);
        }

        [Test]
        public void OffParameterChanged_NotFiredAfterUnsubscribe()
        {
            var ctx = new BaseContext();
            int callCount = 0;
            Action<object> handler = _ => callCount++;
            ctx.OnVariableChanged("score", handler);
            ctx.Set<int>("score", 1);

            ctx.OffVariableChanged("score", handler);
            ctx.Set<int>("score", 2);

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void OnParameterChanged_FiresOnFirstSet()
        {
            var ctx = new BaseContext();
            int calls = 0;
            ctx.OnVariableChanged("hp", _ => calls++);

            ctx.Set<int>("hp", 50);

            Assert.AreEqual(1, calls);
        }

        // ── DeepClone ─────────────────────────────────────────────────────────

        [Test]
        public void DeepClone_CopiesAllValues()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 10);
            ctx.Set<bool>("done", true);

            var clone = ctx.DeepClone();

            Assert.AreEqual(10,   clone.Get<int>("score"));
            Assert.AreEqual(true, clone.Get<bool>("done"));
        }

        [Test]
        public void DeepClone_MutatingOriginalDoesNotAffectClone()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 10);
            var clone = ctx.DeepClone();

            ctx.Set<int>("score", 99);

            Assert.AreEqual(10, clone.Get<int>("score"));
        }

        [Test]
        public void DeepClone_MutatingCloneDoesNotAffectOriginal()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 10);
            var clone = ctx.DeepClone();

            clone.Set<int>("score", 99);

            Assert.AreEqual(10, ctx.Get<int>("score"));
        }

        [Test]
        public void DeepClone_DoesNotCopySubscriptions()
        {
            var ctx = new BaseContext();
            int calls = 0;
            ctx.OnVariableChanged("score", _ => calls++);

            var clone = ctx.DeepClone();
            clone.Set<int>("score", 5);

            Assert.AreEqual(0, calls, "Clone must not carry subscribers from original.");
        }

        [Test]
        public void DeepClone_EmptyContext_ReturnsValidContext()
        {
            var ctx   = new BaseContext();
            var clone = ctx.DeepClone();

            Assert.IsNotNull(clone);
            Assert.IsFalse(clone.Has("anything"));
        }
    }
}
