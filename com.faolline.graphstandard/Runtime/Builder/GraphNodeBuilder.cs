using System;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLogging;


namespace Faolline.GraphStandard
{
    /// <summary>
    /// A fluent handle to a node being built by a <see cref="GraphBuilderBase"/>. Every setter returns the
    /// same handle for chaining. The underlying data is reachable via <see cref="Node"/> (an escape hatch).
    /// <para>
    /// By default every added node gets an auto-generated GUID id. If runtime code (or another graph) addresses
    /// this exact node by a known string — e.g. <c>evaluator.GetState("snowman")</c> or a save key — call
    /// <see cref="Id"/> to give it a stable, readable id (before wiring edges). Easy to forget; the GUID is fine
    /// for nodes you only reach by reference.
    /// </para>
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

        /// <summary>
        /// Overrides the node's auto-GUID id with a stable, readable one (e.g. <c>"room_hub"</c>), so runtime
        /// code can address this exact node of the built/authored graph by a known id. Call before wiring edges
        /// (edges read the id when created). Empty/whitespace is ignored (keeps the GUID).
        /// </summary>
        public GraphNodeBuilder Id(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                Logging.Warning("GraphStandard", "[GraphStandard] GraphNodeBuilder.Id: empty id ignored; keeping the auto-GUID.");
            else
                Node.Id = id;
            return this;
        }

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

        /// <summary>
        /// Parks the runner on this node until ANY of the named signals is raised (logical OR — the runner
        /// resumes on the first one that passes the resume conditions). The code-first counterpart of
        /// graphcore's multi-await: the first name becomes the primary await (unless one is already set),
        /// the rest go to <see cref="BaseNodeData.AwaitSignalNamesExtra"/>, de-duplicated. Null/blank names
        /// log a warning and are skipped.
        /// </summary>
        public GraphNodeBuilder Await(params string[] signalNames)
        {
            if (signalNames == null) return this;
            foreach (var name in signalNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    Logging.Warning("GraphStandard", "[GraphStandard] GraphNodeBuilder.Await: null/blank signal name skipped.");
                    continue;
                }
                if (string.IsNullOrEmpty(Node.AwaitSignalName))
                    Node.AwaitSignalName = name;
                else if (Node.AwaitSignalName != name && !Node.AwaitSignalNamesExtra.Contains(name))
                    Node.AwaitSignalNamesExtra.Add(name);
            }
            return this;
        }

        /// <summary>
        /// Appends resume conditions to this await node: a matching signal resumes the node only if all pass
        /// (AND). A raise that fails the gate is ignored and the node stays parked (re-armable). Pair with
        /// <see cref="Await(string)"/>.
        /// </summary>
        public GraphNodeBuilder ResumeWhen(params BaseCondition[] conditions)
        {
            if (conditions != null) foreach (var c in conditions) if (c != null) Node.ResumeConditions.Add(c);
            return this;
        }

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
                Logging.Warning("GraphStandard", "[GraphStandard] GraphNodeBuilder.Choice: node is not a Choice node; ignored.");
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

        /// <summary>As <see cref="To(GraphNodeBuilder, string)"/>, with a <paramref name="condition"/> gating the edge.</summary>
        public GraphNodeBuilder To(GraphNodeBuilder target, string portName, BaseCondition condition)
        {
            _owner.Edge(this, target, portName, condition);
            return this;
        }
    }
}
