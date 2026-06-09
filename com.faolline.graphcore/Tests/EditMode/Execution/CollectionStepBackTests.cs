using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// US2 (runner layer) — step-back restores exact collection membership: a history snapshot deep-copies
    /// collections, so GoBack drops mutations made after the snapshot.
    /// </summary>
    public class CollectionStepBackTests
    {
        [Test]
        public void GoBack_RestoresCollectionMembership()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                var start = new StartNodeData     { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var mid   = new StatementNodeData { Id = "m", NodeType = StatementNodeData.NodeTypeId };
                var end   = new EndNodeData       { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
                graph.AddNode(start);
                graph.AddNode(mid);
                graph.AddNode(end);
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "m" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "m", ToNodeId = "e" });
                graph.EntryNodeId = "s";

                var ctx    = new BaseContext();
                var runner = new BaseRunner();
                runner.Start(graph, ctx, new NodeExecutorRegistry());   // at s
                ctx.AddToCollection("solved", "p1");
                runner.Proceed();                                       // snapshots {p1}, advances s → m
                ctx.AddToCollection("solved", "p2");                    // live {p1, p2}
                Assert.AreEqual(2, ctx.CollectionCount("solved"));

                runner.GoBack();                                        // restore to the snapshot

                Assert.IsTrue(ctx.CollectionContains("solved", "p1"));
                Assert.IsFalse(ctx.CollectionContains("solved", "p2"), "Step-back drops post-snapshot members.");
                Assert.AreEqual(1, ctx.CollectionCount("solved"));
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
