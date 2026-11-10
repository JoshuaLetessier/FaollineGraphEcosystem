using System.Collections.Generic;
using Faolline.GraphLocalization.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// 054 write-back: <see cref="GraphTextSync.ApplyAll(IEnumerable{GraphTextDrift}, IEnumerable{IGraphLocalizationAdapter}, bool)"/>
    /// routes each drift to its lib's writer, and <see cref="BaseGraphLocalizationAdapter{TGraph}"/> routes a
    /// graph drift to <c>ApplyGraphText</c> and a global one to <c>ApplyGlobalText</c>. The fakes have no
    /// parameterless constructor, so the real build's TypeCache discovery never picks them up.
    /// </summary>
    public class GraphTextSyncApplyTests
    {
        private static GraphTextDrift Drift(string lib, string key, string graphGuid = "g")
            => new GraphTextDrift(lib, graphGuid, graphGuid == null ? null : "G", "T", key, LocalizationKeyType.Text, "", "old", "new");

        private sealed class RecordingWriter : IGraphLocalizationAdapter, IGraphTextWriter
        {
            private readonly bool _result;
            public readonly List<string> Written = new List<string>();
            public RecordingWriter(string libName, bool result) { LibName = libName; _result = result; }
            public string LibName { get; }
            public void ScanAndIndex(LocalizationDatabase database) { }
            public bool WriteGraphText(GraphTextDrift drift) { Written.Add(drift.Key); return _result; }
        }

        private sealed class NoWriter : IGraphLocalizationAdapter
        {
            public NoWriter(string libName) => LibName = libName;
            public string LibName { get; }
            public void ScanAndIndex(LocalizationDatabase database) { }
        }

        [Test]
        public void ApplyAll_RoutesEachDriftToItsLibsWriter_AndCountsTheApplied()
        {
            var a = new RecordingWriter("A", true);
            var b = new RecordingWriter("B", true);

            int applied = GraphTextSync.ApplyAll(
                new[] { Drift("A", "k1"), Drift("B", "k2"), Drift("A", "k3") }, new IGraphLocalizationAdapter[] { a, b }, save: false);

            Assert.AreEqual(3, applied);
            CollectionAssert.AreEqual(new[] { "k1", "k3" }, a.Written);
            CollectionAssert.AreEqual(new[] { "k2" }, b.Written);
        }

        [Test]
        public void ApplyAll_WriterRefusingOrNoWriterOrUnknownLib_IsNotCounted()
        {
            var refusing = new RecordingWriter("A", false);

            int applied = GraphTextSync.ApplyAll(
                new[] { Drift("A", "k1"), Drift("B", "k2"), Drift("C", "k3"), null },
                new IGraphLocalizationAdapter[] { refusing, new NoWriter("B") }, save: false);

            Assert.AreEqual(0, applied);
            CollectionAssert.AreEqual(new[] { "k1" }, refusing.Written);
        }

        [Test]
        public void ApplyAll_NothingToApply_IsZero()
        {
            Assert.AreEqual(0, GraphTextSync.ApplyAll(null, new IGraphLocalizationAdapter[0], save: false));
            Assert.AreEqual(0, GraphTextSync.ApplyAll(new GraphTextDrift[0], null, save: false));
        }

        // ── BaseGraphLocalizationAdapter routing ────────────────────────────────

        private sealed class FakeTextGraph : ScriptableObject { public string Text = "old"; }

        private sealed class FakeAdapter : BaseGraphLocalizationAdapter<FakeTextGraph>
        {
            public readonly List<string> Globals = new List<string>();
            public FakeAdapter(int _) { }
            public override string LibName => "Fake";
            protected override int ExtractGraphKeys(FakeTextGraph graph, LocalizationGraphEntry entry) => 0;

            protected override bool ApplyGraphText(FakeTextGraph graph, GraphTextDrift drift)
            {
                if (drift.Key != "known") return false;
                graph.Text = drift.TableText;
                return true;
            }

            protected override bool ApplyGlobalText(GraphTextDrift drift)
            {
                Globals.Add(drift.Key);
                return true;
            }
        }

        private sealed class BareAdapter : BaseGraphLocalizationAdapter<FakeTextGraph>
        {
            public BareAdapter(int _) { }
            public override string LibName => "Bare";
            protected override int ExtractGraphKeys(FakeTextGraph graph, LocalizationGraphEntry entry) => 0;
        }

        [Test]
        public void BaseAdapter_GraphDrift_GoesToApplyGraphText()
        {
            var graph = ScriptableObject.CreateInstance<FakeTextGraph>();
            try
            {
                var adapter = new FakeAdapter(0);
                Assert.IsTrue(adapter.WriteGraphText(graph, Drift("Fake", "known")));
                Assert.AreEqual("new", graph.Text);

                Assert.IsFalse(adapter.WriteGraphText(graph, Drift("Fake", "unknown")));
                Assert.IsFalse(adapter.WriteGraphText(null, Drift("Fake", "known")));
                Assert.IsFalse(adapter.WriteGraphText(graph, null));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void BaseAdapter_GlobalDrift_GoesToApplyGlobalText()
        {
            var adapter = new FakeAdapter(0);
            Assert.IsTrue(adapter.WriteGraphText(Drift("Fake", "speaker_x", graphGuid: null)));
            CollectionAssert.AreEqual(new[] { "speaker_x" }, adapter.Globals);
        }

        [Test]
        public void BaseAdapter_GraphDriftWithAnUnknownGuid_IsNotApplied()
        {
            Assert.IsFalse(new FakeAdapter(0).WriteGraphText(Drift("Fake", "known", graphGuid: "00000000000000000000000000000000")));
        }

        [Test]
        public void BaseAdapter_WithoutHooks_AppliesNothing()
        {
            var graph = ScriptableObject.CreateInstance<FakeTextGraph>();
            try
            {
                var adapter = new BareAdapter(0);
                Assert.IsFalse(adapter.WriteGraphText(graph, Drift("Bare", "known")));
                Assert.IsFalse(adapter.WriteGraphText(Drift("Bare", "speaker_x", graphGuid: null)));
                Assert.AreEqual("old", graph.Text);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
