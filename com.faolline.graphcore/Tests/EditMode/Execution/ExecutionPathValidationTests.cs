using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Faolline.GraphCore.Tests.ExecutionPathFixture;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// <see cref="GraphRunPath.TryResolve(BaseGraph, IReadOnlyList{GraphRunFrame}, out GraphRunPathError)"/>: the pure
    /// check of a saved execution path against the graphs it walks — every mismatch reason with the level where it
    /// occurs and the deepest level still valid, and no side effect.
    /// </summary>
    public class ExecutionPathValidationTests
    {
        private ExecutionPathFixture _f;
        private BaseGraph _c, _z, _r;

        // C: c-start → c-sub(→Z) → c-end
        // Z: z-start → z-sub(→R) → z-end
        // R: r-start → r-mid → r-end
        [SetUp]
        public void SetUp()
        {
            _f = new ExecutionPathFixture();
            _r = _f.Chain(Start("r-start"), Step("r-mid"), End("r-end"));
            _z = _f.Chain(Start("z-start"), Sub("z-sub", _r), End("z-end"));
            _c = _f.Chain(Start("c-start"), Sub("c-sub", _z), End("c-end"));
        }

        [TearDown]
        public void TearDown() => _f.Dispose();

        private static List<GraphRunFrame> Path(params GraphRunFrame[] frames) => new List<GraphRunFrame>(frames);

        private static GraphRunPathError Fails(BaseGraph root, List<GraphRunFrame> path, GraphRunPathErrorReason reason,
                                               int frameIndex, int deepestValid)
        {
            Assert.IsFalse(GraphRunPath.TryResolve(root, path, out var error), "the path must be rejected");
            Assert.IsNotNull(error);
            Assert.AreEqual(reason, error.Reason);
            Assert.AreEqual(frameIndex, error.FrameIndex, "FrameIndex");
            Assert.AreEqual(deepestValid, error.DeepestValidFrameIndex, "DeepestValidFrameIndex");
            Assert.IsFalse(string.IsNullOrEmpty(error.Message));
            Assert.AreEqual(error.Message, error.ToString());
            return error;
        }

        // ── Valid paths ──────────────────────────────────────────────────────────

        [Test]
        public void ThreeLevelPath_ResolvesEveryLevelsGraph()
        {
            var ok = GraphRunPath.TryResolve(_c, Path(Frame(_c, "c-sub"), Frame(_z, "z-sub"), Frame(_r, "r-mid")),
                                             out var graphs, out var error);

            Assert.IsTrue(ok);
            Assert.IsNull(error);
            CollectionAssert.AreEqual(new[] { _c, _z, _r }, graphs);
        }

        [Test]
        public void SingleLevelPath_ResolvesTheRoot()
        {
            Assert.IsTrue(GraphRunPath.TryResolve(_c, Path(Frame(_c, "c-end")), out var graphs, out _));
            CollectionAssert.AreEqual(new[] { _c }, graphs);
        }

        [Test]
        public void DeepestLevelOnASubGraphNode_IsValid()
        {
            // Captured on OnNodeEntered of a SubGraph checkpoint, before Proceed entered the child.
            Assert.IsTrue(GraphRunPath.TryResolve(_c, Path(Frame(_c, "c-sub")), out _));
        }

        [Test]
        public void ContextModesMatchingTheNodes_AreValid()
        {
            var r = _f.Chain(Start("r2-start"), End("r2-end"));
            var z = _f.Chain(Start("z2-start"), Sub("z2-sub", r, GraphRunContextMode.Fresh), End("z2-end"));
            var c = _f.Chain(Start("c2-start"), Sub("c2-sub", z, GraphRunContextMode.Scoped), End("c2-end"));

            Assert.IsTrue(GraphRunPath.TryResolve(c, Path(
                Frame(c, "c2-sub"),
                Frame(z, "z2-sub", GraphRunContextMode.Scoped),
                Frame(r, "r2-start", GraphRunContextMode.Fresh)), out _));
        }

        // ── Mismatches ───────────────────────────────────────────────────────────

        [Test]
        public void NullRoot_IsReported()
            => Fails(null, Path(Frame(_c, "c-start")), GraphRunPathErrorReason.NullRootGraph, 0, -1);

        [Test]
        public void EmptyOrNullPath_IsReported()
        {
            Fails(_c, Path(), GraphRunPathErrorReason.EmptyPath, -1, -1);
            Fails(_c, null, GraphRunPathErrorReason.EmptyPath, -1, -1);
        }

        [Test]
        public void WrongRoot_IsReported_WithBothGraphIds()
        {
            var error = Fails(_z, Path(Frame(_c, "c-sub"), Frame(_z, "z-start")), GraphRunPathErrorReason.RootGraphMismatch, 0, -1);
            StringAssert.Contains(_c.GraphId, error.Message);
            StringAssert.Contains(_z.GraphId, error.Message);
        }

        [Test]
        public void DeletedNode_IsReported_AtItsLevel()
        {
            var error = Fails(_c, Path(Frame(_c, "c-sub"), Frame(_z, "gone")), GraphRunPathErrorReason.NodeMissing, 1, 0);
            StringAssert.Contains("Frame 1", error.Message);
            StringAssert.Contains("gone", error.Message);
        }

        [Test]
        public void DeletedNode_AtThirdLevel_KeepsTheTwoLevelsAboveValid()
            => Fails(_c, Path(Frame(_c, "c-sub"), Frame(_z, "z-sub"), Frame(_r, "gone")), GraphRunPathErrorReason.NodeMissing, 2, 1);

        [Test]
        public void EmptyOrNullNodeId_IsReportedAsMissing_WithoutThrowing()
        {
            Fails(_c, Path(Frame(_c, "")), GraphRunPathErrorReason.NodeMissing, 0, -1);
            Fails(_c, Path(new GraphRunFrame(_c.GraphId, null)), GraphRunPathErrorReason.NodeMissing, 0, -1);
        }

        [Test]
        public void NullFrame_IsReportedAsMissing()
            => Fails(_c, Path(Frame(_c, "c-sub"), null), GraphRunPathErrorReason.NodeMissing, 1, 0);

        [Test]
        public void EnclosingLevelNoLongerOnASubGraphNode_IsReported()
            => Fails(_c, Path(Frame(_c, "c-start"), Frame(_z, "z-start")), GraphRunPathErrorReason.NotASubGraphNode, 0, 0);

        [Test]
        public void SubGraphNodeWithoutTarget_IsReported()
        {
            ((SubGraphNodeData)FindNode(_c, "c-sub")).TargetGraph = null;
            Fails(_c, Path(Frame(_c, "c-sub"), Frame(_z, "z-start")), GraphRunPathErrorReason.SubGraphTargetMissing, 0, 0);
        }

        [Test]
        public void RetargetedSubGraphNode_IsReported()
        {
            var other = _f.Chain(Start("o-start"), End("o-end"));
            ((SubGraphNodeData)FindNode(_c, "c-sub")).TargetGraph = other;
            var error = Fails(_c, Path(Frame(_c, "c-sub"), Frame(_z, "z-start")), GraphRunPathErrorReason.SubGraphTargetMismatch, 0, 0);
            StringAssert.Contains(_z.GraphId, error.Message);
            StringAssert.Contains(other.GraphId, error.Message);
        }

        [Test]
        public void SubGraphNodeWhoseContextModeChanged_IsReported()
            => Fails(_c, Path(Frame(_c, "c-sub"), Frame(_z, "z-start", GraphRunContextMode.Scoped)),
                     GraphRunPathErrorReason.ContextModeMismatch, 0, 0);

        [Test]
        public void RepeatedGraph_IsReportedAsAMismatch_NotAsACycleException()
        {
            // Cyclic content A → B → A: a live run would throw GraphCycleException on the second entry; a saved path
            // naming A twice can only be corrupted or edited.
            var a = _f.Chain(Start("a-start"), Sub("a-sub", null), End("a-end"));
            var b = _f.Chain(Start("b-start"), Sub("b-sub", a), End("b-end"));
            ((SubGraphNodeData)FindNode(a, "a-sub")).TargetGraph = b;

            Fails(a, Path(Frame(a, "a-sub"), Frame(b, "b-sub"), Frame(a, "a-start")), GraphRunPathErrorReason.RepeatedGraph, 2, 1);
        }

        // ── Purity ──────────────────────────────────────────────────────────────

        [Test]
        public void Check_ModifiesNothing_AndLogsNothing()
        {
            int nodes = _c.Nodes.Count, edges = _c.Edges.Count;

            GraphRunPath.TryResolve(_c, Path(Frame(_c, "c-sub"), Frame(_z, "gone")), out _);
            GraphRunPath.TryResolve(_c, Path(Frame(_c, "c-sub"), Frame(_z, "z-sub"), Frame(_r, "r-mid")), out _);

            Assert.AreEqual(nodes, _c.Nodes.Count);
            Assert.AreEqual(edges, _c.Edges.Count);
            Assert.AreSame(_z, ((SubGraphNodeData)FindNode(_c, "c-sub")).TargetGraph);
            LogAssert.NoUnexpectedReceived();
        }

        private static BaseNodeData FindNode(BaseGraph g, string id)
        {
            foreach (var n in g.Nodes) if (n.Id == id) return n;
            return null;
        }
    }
}
