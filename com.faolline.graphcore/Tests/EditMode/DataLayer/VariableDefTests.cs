using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class VariableDefTests
    {
        // ── VariableType enum ────────────────────────────────────────────────

        [Test]
        public void VariableType_HasExactlySevenValues()
        {
            // The four primitives plus the three serialization-friendly Unity value types.
            // Object/GameObject references are intentionally excluded (they need a stable-id scheme).
            Assert.AreEqual(7, System.Enum.GetValues(typeof(VariableType)).Length,
                "VariableType must have exactly 7 values (bool/int/float/string + Vector2/Vector3/Color).");
        }

        [Test]
        public void VariableType_HasCorrectIntegerValues()
        {
            Assert.AreEqual(0, (int)VariableType.Bool);
            Assert.AreEqual(1, (int)VariableType.Int);
            Assert.AreEqual(2, (int)VariableType.Float);
            Assert.AreEqual(3, (int)VariableType.String);
            Assert.AreEqual(4, (int)VariableType.Vector2);
            Assert.AreEqual(5, (int)VariableType.Vector3);
            Assert.AreEqual(6, (int)VariableType.Color);
        }

        // ── VariableDef identity ────────────────────────────────────────────

        [Test]
        public void Factory_AssignsGuidKey_TypeAndDefault()
        {
            var p = VariableDef.Int("Hp", 100);
            try
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.Key), "Key (GUID) is assigned in OnEnable.");
                Assert.AreEqual("Hp", p.DisplayName);
                Assert.AreEqual(VariableType.Int, p.Type);
                Assert.AreEqual(100, p.DefaultValueBoxed);
                Assert.AreEqual(p.Key, (string)p, "implicit string conversion yields the GUID key.");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void TwoFactoryCalls_SameDisplayName_HaveDistinctGuids()
        {
            var a = VariableDef.Bool("flag");
            var b = VariableDef.Bool("flag");
            try
            {
                Assert.AreNotEqual(a.Key, b.Key, "identity is the GUID, not the display name.");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void DisplayName_FallsBackToAssetName_WhenNoLabel()
        {
            var p = ScriptableObject.CreateInstance<VariableDef>();
            p.name = "AssetFileName";
            try { Assert.AreEqual("AssetFileName", p.DisplayName); }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void ImplementsStableGuidIdentity()
        {
            var p = VariableDef.Float("speed", 1.5f);
            try
            {
                var id = (IStableGuidIdentity)p;
                Assert.AreEqual(p.Key, id.StableId);
                Assert.AreEqual("_id", id.StableIdFieldName);
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void DefaultValueBoxed_MatchesType()
        {
            var s = VariableDef.String("name", "Hero");
            var v = VariableDef.Vector2("pos", new Vector2(1, 2));
            var c = VariableDef.Color("tint", Color.red);
            try
            {
                Assert.AreEqual("Hero", s.DefaultValueBoxed);
                Assert.AreEqual(new Vector2(1, 2), v.DefaultValueBoxed);
                Assert.AreEqual(Color.red, c.DefaultValueBoxed);
            }
            finally { Object.DestroyImmediate(s); Object.DestroyImmediate(v); Object.DestroyImmediate(c); }
        }

        [Test]
        public void NullVariableDef_ImplicitString_IsEmpty()
        {
            VariableDef p = null;
            Assert.AreEqual(string.Empty, (string)p);
        }
    }
}
