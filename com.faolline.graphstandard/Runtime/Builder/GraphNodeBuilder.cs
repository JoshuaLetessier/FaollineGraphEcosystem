using System;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// A fluent handle to a node being built by a <see cref="GraphBuilderBase"/>. Every setter returns the
    /// same handle for chaining. The underlying data is reachable via <see cref="Node"/> (an escape hatch).
    /// </summary>
    public sealed class GraphNodeBuilder
    {
        private readonly GraphBuilderBase _owner;

        /// <summary>The node data this handle configures.</summary>
        public BaseNodeData Node { get; }

        internal GraphNodeBuilder(GraphBuilderBase owner, BaseNodeData node)
        {
            _owner = owner;
            Node   = node;
        }

        /// <summary>Sets the node's title.</summary>
        public GraphNodeBuilder Title(string title) { Node.Title = title; return this; }

        /// <summary>Sets the node's canvas position.</summary>
        public GraphNodeBuilder At(float x, float y) { Node.Position = new Vector2(x, y); return this; }

        /// <summary>Sets the node's canvas position.</summary>
        public GraphNodeBuilder At(Vector2 position) { Node.Position = position; return this; }

        /// <summary>Appends enter-actions (run on entering the node).</summary>
        public GraphNodeBuilder OnEnter(params BaseAction[] actions)
        {
            if (actions != null) foreach (var a in actions) if (a != null) Node.OnEnterActions.Add(a);
            return this;
        }

        /// <summary>Appends exit-actions (run on leaving the node).</summary>
        public GraphNodeBuilder OnExit(params BaseAction[] actions)
        {
            if (actions != null) foreach (var a in actions) if (a != null) Node.OnExitActions.Add(a);
            return this;
        }

        /// <summary>Appends entry conditions (all must pass to enter the node).</summary>
        public GraphNodeBuilder When(params BaseCondition[] conditions)
        {
            if (conditions != null) foreach (var c in conditions) if (c != null) Node.EntryConditions.Add(c);
            return this;
        }

        /// <summary>Parks the runner on this node until the named signal is raised.</summary>
        public GraphNodeBuilder Await(string signalName) { Node.AwaitSignalName = signalName; return this; }

        /// <summary>Holds on this node for the given seconds of host-fed time before advancing.</summary>
        public GraphNodeBuilder Wait(float seconds) { Node.WaitDuration = seconds; return this; }

        /// <summary>Marks (or clears) this node as a checkpoint for GoBackToCheckpoint.</summary>
        public GraphNodeBuilder Checkpoint(bool value = true) { Node.IsCheckpoint = value; return this; }

        /// <summary>
        /// Adds a choice (with a human <paramref name="title"/> and optional gating condition) to a Choice
        /// node; logs and ignores on a non-Choice node. Wire a choice edge with
        /// <c>Edge(choiceNode, target, title)</c>.
        /// </summary>
        public GraphNodeBuilder Choice(string title, BaseCondition condition = null)
        {
            if (Node is ChoiceNodeData choice)
                choice.Choices.Add(new BaseChoice
                {
                    Id        = Guid.NewGuid().ToString("D"),
                    Title     = title ?? string.Empty,
                    Condition = condition
                });
            else
                Debug.LogWarning("[GraphStandard] GraphNodeBuilder.Choice: node is not a Choice node; ignored.");
            return this;
        }

        /// <summary>Designates this node as the graph's entry node.</summary>
        public GraphNodeBuilder AsEntry() { _owner.SetEntry(this); return this; }

        /// <summary>Connects this node to <paramref name="target"/> (sugar for the builder's Edge).</summary>
        public GraphNodeBuilder To(GraphNodeBuilder target, string portName = "out")
        {
            _owner.Edge(this, target, portName);
            return this;
        }
    }
}
