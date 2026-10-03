using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Code-built graphs for the execution-path tests: each graph is a straight chain of nodes in the order given
    /// (entry = first node), and every object created is destroyed on <see cref="Dispose"/>.
    /// </summary>
    internal sealed class ExecutionPathFixture : IDisposable
    {
        private readonly List<Object> _objects = new List<Object>();

        public BaseGraph Chain(params BaseNodeData[] nodes)
        {
            var g = Track(ScriptableObject.CreateInstance<BaseGraph>());
            for (int i = 0; i < nodes.Length; i++)
            {
                g.AddNode(nodes[i]);
                if (i > 0)
                    g.AddEdge(new BaseEdgeData { Id = $"e-{nodes[i - 1].Id}-{nodes[i].Id}", FromNodeId = nodes[i - 1].Id, ToNodeId = nodes[i].Id });
            }
            g.EntryNodeId = nodes[0].Id;
            return g;
        }

        public static StartNodeData Start(string id) => new StartNodeData { Id = id, NodeType = StartNodeData.NodeTypeId };
        public static StatementNodeData Step(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };
        public static EndNodeData End(string id) => new EndNodeData { Id = id, NodeType = EndNodeData.NodeTypeId };

        public static SubGraphNodeData Sub(string id, BaseGraph target, GraphRunContextMode mode = GraphRunContextMode.Shared)
            => new SubGraphNodeData
            {
                Id                   = id,
                NodeType             = SubGraphNodeData.NodeTypeId,
                TargetGraph          = target,
                OpensScope           = mode == GraphRunContextMode.Scoped,
                InheritParentContext = mode == GraphRunContextMode.Shared
            };

        public static GraphRunFrame Frame(BaseGraph graph, string nodeId, GraphRunContextMode mode = GraphRunContextMode.Shared)
            => new GraphRunFrame(graph.GraphId, nodeId, mode);

        /// <summary>An enter action that counts its executions.</summary>
        public CountingAction Counter() => Track(ScriptableObject.CreateInstance<CountingAction>());

        /// <summary>
        /// Makes <paramref name="node"/> reference <paramref name="variable"/> through an inert resume condition, so
        /// <c>InitFromGraph</c>/<c>BeginLocalContext(graph)</c> discover and seed its default.
        /// </summary>
        public void Reference(BaseNodeData node, VariableDef variable)
        {
            var probe = Track(ScriptableObject.CreateInstance<IntCondition>());
            probe.Variable = variable;
            node.ResumeConditions.Add(probe);
        }

        public VariableDef IntVariable(string name, int defaultValue) => Track(VariableDef.Int(name, defaultValue));

        public T Track<T>(T o) where T : Object { _objects.Add(o); return o; }

        public void Dispose()
        {
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }
    }

    internal sealed class CountingAction : BaseAction
    {
        public int Count;
        public override void Execute(BaseContext context) => Count++;
    }

    internal sealed class LambdaNodeExecutor : INodeExecutor
    {
        private readonly Action<BaseNodeData, BaseContext> _exec;
        public string NodeType { get; }
        public LambdaNodeExecutor(string type, Action<BaseNodeData, BaseContext> exec) { NodeType = type; _exec = exec; }
        public void Execute(BaseNodeData node, BaseContext context) => _exec(node, context);
    }
}
