using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph.Tests
{
    /// <summary>US1 — the typed-context contract (Principle VI): typed props, clone subtype, GoBack restore.</summary>
    [TestFixture]
    public class StarterContextContractTests
    {
        [Test]
        public void TypedProperties_RoundTrip()
        {
            var ctx = new StarterContext { Flag = true, Score = 7, Ratio = 0.25f, Label = "hero" };
            Assert.IsTrue(ctx.Flag);
            Assert.AreEqual(7, ctx.Score);
            Assert.AreEqual(0.25f, ctx.Ratio, 0.0001f);
            Assert.AreEqual("hero", ctx.Label);
        }

        [Test]
        public void DeepClone_ReturnsSubtype_WithValues()
        {
            var ctx = new StarterContext { Flag = true, Score = 9, Ratio = 1.5f, Label = "x" };
            var clone = ctx.DeepClone();
            Assert.IsInstanceOf<StarterContext>(clone,
                "CreateCloneInstance() must return StarterContext, else GoBack history restore breaks");
            var typed = (StarterContext)clone;
            Assert.IsTrue(typed.Flag);
            Assert.AreEqual(9, typed.Score);
            Assert.AreEqual(1.5f, typed.Ratio, 0.0001f);
            Assert.AreEqual("x", typed.Label);
        }

        [Test]
        public void GoBack_RestoresTypedValues_AcrossInt_Float_String()
        {
            var setInt   = ScriptableObject.CreateInstance<StarterSetIntAction>();    setInt.ParameterKey   = StarterContextKeys.Score; setInt.Value   = 99;
            var setFloat = ScriptableObject.CreateInstance<StarterSetFloatAction>();  setFloat.ParameterKey = StarterContextKeys.Ratio; setFloat.Value = 1.5f;
            var setStr   = ScriptableObject.CreateInstance<StarterSetStringAction>(); setStr.ParameterKey   = StarterContextKeys.Label; setStr.Value   = "boss";
            var g = ScriptableObject.CreateInstance<StarterGraph>();
            try
            {
                var start = new StartNodeData            { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var a     = new StarterStatementNodeData { Id = "a", NodeType = StarterStatementNodeData.NodeTypeId };
                a.OnEnterActions.Add(setInt); a.OnEnterActions.Add(setFloat); a.OnEnterActions.Add(setStr);
                var b   = new StarterStatementNodeData { Id = "b",   NodeType = StarterStatementNodeData.NodeTypeId };
                var end = new EndNodeData              { Id = "end", NodeType = EndNodeData.NodeTypeId };
                g.AddNode(start); g.AddNode(a); g.AddNode(b); g.AddNode(end); g.EntryNodeId = "s";
                g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "a",   PortName = "out" });
                g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "a", ToNodeId = "b",   PortName = "out" });
                g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "b", ToNodeId = "end", PortName = "out" });

                var ctx = new StarterContext();
                var runner = new BaseRunner();
                runner.Start(g, ctx, new NodeExecutorRegistry());
                int guard = 0; while (runner.State == RunnerState.NodeReady && guard++ < 100) runner.Proceed();

                Assert.AreEqual(99, ctx.Score);
                Assert.AreEqual(1.5f, ctx.Ratio, 0.0001f);
                Assert.AreEqual("boss", ctx.Label);

                string last = null; guard = 0;
                while (guard++ < 100) { runner.GoBack(); var id = runner.CurrentNode?.Id; if (id == "s" || id == last) break; last = id; }

                Assert.AreEqual("s", runner.CurrentNode?.Id);
                Assert.IsFalse(ctx.Has(StarterContextKeys.Score), "GoBack to Start reverts the int");
                Assert.IsFalse(ctx.Has(StarterContextKeys.Ratio), "GoBack to Start reverts the float");
                Assert.IsFalse(ctx.Has(StarterContextKeys.Label), "GoBack to Start reverts the string");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(setInt);
                UnityEngine.Object.DestroyImmediate(setFloat);
                UnityEngine.Object.DestroyImmediate(setStr);
                UnityEngine.Object.DestroyImmediate(g);
            }
        }
    }
}
