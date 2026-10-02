using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// <see cref="TableScopedLookup"/> dispatch (052, research R3): a table-aware provider is asked in the
    /// designated table; anything else — a plain provider, or no table — goes through the classic call,
    /// so existing providers keep working unchanged.
    /// </summary>
    public class TableScopedLookupTests
    {
        private class PlainProvider : ILocalizationProvider
        {
            public readonly List<string> ResolveCalls = new List<string>();
            public string CurrentLocale => "en";
            public void SetLocale(string locale) { }
            public string Resolve(string key, string locale) { ResolveCalls.Add(key); return key == "known" ? "Plain" : "#" + key; }
        }

        private sealed class ScopedProvider : PlainProvider, ITableScopedLocalizationProvider
        {
            public readonly List<(string table, string key)> ScopedCalls = new List<(string, string)>();
            public string ResolveInTable(string table, string key, string locale)
            {
                ScopedCalls.Add((table, key));
                return key == "known" ? "Scoped" : "#" + key;
            }
        }

        private class PlainAssets : ILocalizedAssetProvider
        {
            public readonly List<string> ResolveCalls = new List<string>();
            public T ResolveAsset<T>(string key) where T : Object { ResolveCalls.Add(key); return null; }
        }

        private sealed class ScopedAssets : PlainAssets, ITableScopedLocalizedAssetProvider
        {
            public readonly List<(string table, string key)> ScopedCalls = new List<(string, string)>();
            public T ResolveAssetInTable<T>(string table, string key) where T : Object { ScopedCalls.Add((table, key)); return null; }
        }

        [Test]
        public void ScopedProvider_WithTable_UsesResolveInTableOnly()
        {
            var p = new ScopedProvider();
            Assert.AreEqual("Scoped", TableScopedLookup.Resolve(p, "DLG_001", "known", "en"));
            CollectionAssert.AreEqual(new[] { ("DLG_001", "known") }, p.ScopedCalls);
            Assert.IsEmpty(p.ResolveCalls);
        }

        [TestCase(null)]
        [TestCase("")]
        public void ScopedProvider_WithoutTable_UsesClassicResolve(string table)
        {
            var p = new ScopedProvider();
            Assert.AreEqual("Plain", TableScopedLookup.Resolve(p, table, "known", "en"));
            Assert.IsEmpty(p.ScopedCalls);
            CollectionAssert.AreEqual(new[] { "known" }, p.ResolveCalls);
        }

        [Test]
        public void PlainProvider_AlwaysUsesClassicResolve()
        {
            var p = new PlainProvider();
            Assert.AreEqual("Plain", TableScopedLookup.Resolve(p, "DLG_001", "known", "en"));
            CollectionAssert.AreEqual(new[] { "known" }, p.ResolveCalls);
        }

        [Test]
        public void MissingKey_MarkerPassesThroughUnchanged()
        {
            Assert.AreEqual("#nope", TableScopedLookup.Resolve(new ScopedProvider(), "DLG_001", "nope", "en"));
            Assert.AreEqual("#nope", TableScopedLookup.Resolve(new PlainProvider(), "DLG_001", "nope", "en"));
        }

        [Test]
        public void NullProvider_ReturnsMarker()
            => Assert.AreEqual("#k", TableScopedLookup.Resolve(null, "DLG_001", "k", "en"));

        [Test]
        public void Assets_ScopedWithTable_UsesResolveAssetInTableOnly()
        {
            var a = new ScopedAssets();
            TableScopedLookup.ResolveAsset<AudioClip>(a, "DLG_001", "line_x");
            CollectionAssert.AreEqual(new[] { ("DLG_001", "line_x") }, a.ScopedCalls);
            Assert.IsEmpty(a.ResolveCalls);
        }

        [Test]
        public void Assets_ScopedWithoutTable_UsesClassicResolveAsset()
        {
            var a = new ScopedAssets();
            TableScopedLookup.ResolveAsset<AudioClip>(a, null, "line_x");
            Assert.IsEmpty(a.ScopedCalls);
            CollectionAssert.AreEqual(new[] { "line_x" }, a.ResolveCalls);
        }

        [Test]
        public void Assets_Plain_UsesClassicResolveAsset()
        {
            var a = new PlainAssets();
            TableScopedLookup.ResolveAsset<AudioClip>(a, "DLG_001", "line_x");
            CollectionAssert.AreEqual(new[] { "line_x" }, a.ResolveCalls);
        }

        [Test]
        public void Assets_NullProvider_ReturnsNull()
            => Assert.IsNull(TableScopedLookup.ResolveAsset<AudioClip>(null, "DLG_001", "line_x"));
    }
}
