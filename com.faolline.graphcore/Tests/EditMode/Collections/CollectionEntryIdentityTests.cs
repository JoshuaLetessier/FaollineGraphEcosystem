using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// <see cref="CollectionEntry.Key"/> / <see cref="CollectionDef.Key"/> must be stable, non-editable
    /// GUIDs (mirroring <see cref="BaseGraph.GraphId"/>) — never derived from the mutable asset name.
    /// </summary>
    public class CollectionEntryIdentityTests
    {
        [Test]
        public void CollectionEntry_Key_IsAssignedOnEnable_NeverEmpty()
        {
            var e = ScriptableObject.CreateInstance<CollectionEntry>();
            Assert.IsFalse(string.IsNullOrEmpty(e.Key));
            Object.DestroyImmediate(e);
        }

        [Test]
        public void CollectionEntry_Key_IsIndependentOfAssetName()
        {
            var a = ScriptableObject.CreateInstance<CollectionEntry>(); a.name = "sword";
            var b = ScriptableObject.CreateInstance<CollectionEntry>(); b.name = "sword"; // same name, different asset
            Assert.AreNotEqual(a.Key, b.Key, "two independently-created entries must never collide by name");
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        [Test]
        public void CollectionEntry_Key_UnaffectedByRename()
        {
            var e = ScriptableObject.CreateInstance<CollectionEntry>();
            var key = e.Key;
            e.name = "renamed";
            Assert.AreEqual(key, e.Key, "renaming the asset must not change its stored key");
            Object.DestroyImmediate(e);
        }

        [Test]
        public void CollectionEntry_Title_FallsBackToAssetName_NeverAffectsKey()
        {
            var e = ScriptableObject.CreateInstance<CollectionEntry>(); e.name = "Sword";
            var key = e.Key;
            Assert.AreEqual("Sword", e.Title);
            Assert.AreEqual(key, e.Key, "Title is purely cosmetic");
            Object.DestroyImmediate(e);
        }

        [Test]
        public void CollectionEntry_ImplicitStringConversion_ReturnsKey()
        {
            var e = ScriptableObject.CreateInstance<CollectionEntry>();
            string s = e;
            Assert.AreEqual(e.Key, s);
            Object.DestroyImmediate(e);
        }

        [Test]
        public void CollectionName_Key_IsAssignedOnEnable_NeverEmpty()
        {
            var c = ScriptableObject.CreateInstance<CollectionDef>();
            Assert.IsFalse(string.IsNullOrEmpty(c.Key));
            Object.DestroyImmediate(c);
        }

        [Test]
        public void CollectionName_Key_IsIndependentOfAssetName()
        {
            var a = ScriptableObject.CreateInstance<CollectionDef>(); a.name = "inventory";
            var b = ScriptableObject.CreateInstance<CollectionDef>(); b.name = "inventory";
            Assert.AreNotEqual(a.Key, b.Key);
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        [Test]
        public void CollectionName_Key_UnaffectedByRename()
        {
            var c = ScriptableObject.CreateInstance<CollectionDef>();
            var key = c.Key;
            c.name = "renamed";
            Assert.AreEqual(key, c.Key);
            Object.DestroyImmediate(c);
        }
    }
}
