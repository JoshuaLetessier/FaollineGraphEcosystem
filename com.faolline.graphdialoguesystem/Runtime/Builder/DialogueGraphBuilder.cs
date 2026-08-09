using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Fluent, code-first construction of a <see cref="DialogueGraph"/> — the dialogue counterpart of
    /// graphstandard's <c>GraphBuilder</c> (which only makes universal nodes, so a plain statement would be
    /// silently drained instead of spoken). Build lines and choices directly:
    /// <code>
    /// var b = new DialogueGraphBuilder();
    /// var hi  = b.AddLine("guardian", "Bonjour, aventurier").AsEntry();
    /// var hub = b.AddChoice();
    /// var ask = b.AddLine("guardian").Say("La ville est ancienne.");
    /// var end = b.AddEnd();
    /// hi.To(hub);
    /// hub.Option("Demander").To(ask);
    /// hub.Option("Partir").To(end);
    /// ask.To(end);
    /// DialogueGraph graph = b.Build();
    /// </code>
    /// The right node types (<see cref="DialogueLineNodeData"/>, <see cref="ChoiceNodeData"/> +
    /// <see cref="DialogueChoice"/>) and their <c>NodeType</c> ids are set for you, so a built dialogue plays
    /// correctly with no hand-assembly. Pair with <see cref="DialogueTitleProvider"/> to render a built dialogue
    /// with no localization table.
    /// </summary>
    public sealed class DialogueGraphBuilder
    {
        private readonly List<BaseNodeData> _nodes = new List<BaseNodeData>();
        private readonly List<BaseEdgeData> _edges = new List<BaseEdgeData>();
        private readonly List<Speaker> _speakers = new List<Speaker>();
        private readonly HashSet<DialogueNodeHandle> _handles = new HashSet<DialogueNodeHandle>();
        private DialogueNodeHandle _entry;
        private int _column;

        /// <summary>Adds a spoken line node for <paramref name="speakerKey"/>; set its text with <c>.Say(...)</c>
        /// or the optional <paramref name="text"/>.</summary>
        public DialogueLineHandle AddLine(string speakerKey = null, string text = null)
        {
            var node = new DialogueLineNodeData
            {
                NodeType = DialogueLineNodeData.NodeTypeId,
                SpeakerKey = speakerKey ?? string.Empty
            };
            if (!string.IsNullOrEmpty(text)) node.Title = text;
            Place(node);
            return new DialogueLineHandle(this, node);
        }

        /// <summary>Adds a branching choice node; add options with <c>.Option(label).To(target)</c>.</summary>
        public DialogueChoiceHandle AddChoice(string title = null)
        {
            var node = new ChoiceNodeData { NodeType = ChoiceNodeData.NodeTypeId };
            if (!string.IsNullOrEmpty(title)) node.Title = title;
            Place(node);
            return new DialogueChoiceHandle(this, node);
        }

        /// <summary>Adds an End node carrying <paramref name="reason"/> and an optional
        /// <paramref name="outcomeLabel"/> (e.g. "persuaded", "rejected").</summary>
        public DialogueBasicHandle AddEnd(EndReason reason = EndReason.Completed, string outcomeLabel = null)
        {
            var node = new EndNodeData { NodeType = EndNodeData.NodeTypeId, EndReason = reason };
            if (!string.IsNullOrEmpty(outcomeLabel)) node.OutcomeLabel = outcomeLabel;
            Place(node);
            return new DialogueBasicHandle(this, node);
        }

        /// <summary>Adds a SubGraph node delegating to <paramref name="target"/> — a jump into another,
        /// separately-authored graph (e.g. a reusable sub-conversation), mirroring graphstandard's
        /// GraphBuilderBase.AddSubGraph. A null <paramref name="target"/> is a valid, documented
        /// "incomplete node" state (graphcore skips it with a runtime warning), never an error here.</summary>
        public DialogueSubGraphHandle AddSubGraph(string title = null, BaseGraph target = null)
        {
            var node = new SubGraphNodeData { NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = target };
            if (!string.IsNullOrEmpty(title)) node.Title = title;
            Place(node);
            return new DialogueSubGraphHandle(this, node);
        }

        /// <summary>Registers a speaker on the built graph (so the scene needs no separate speaker list).</summary>
        public DialogueGraphBuilder WithSpeaker(Speaker speaker)
        {
            if (speaker != null && !_speakers.Contains(speaker)) _speakers.Add(speaker);
            return this;
        }

        /// <summary>Materialises the accumulated nodes/edges/speakers into a fresh <see cref="DialogueGraph"/> asset.</summary>
        public DialogueGraph Build()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            foreach (var n in _nodes) graph.AddNode(n);
            foreach (var e in _edges) graph.AddEdge(e);
            if (_entry != null) graph.EntryNodeId = _entry.Node.Id;
            foreach (var s in _speakers) graph.AddSpeaker(s);
            return graph;
        }

        // ── Internal wiring (called by the handles) ─────────────────────────────────
        internal void Track(DialogueNodeHandle handle) => _handles.Add(handle);
        internal void SetEntry(DialogueNodeHandle handle) => _entry = handle;

        internal void Connect(DialogueNodeHandle from, DialogueNodeHandle to, string portName)
        {
            if (from == null || to == null)
                throw new ArgumentNullException(from == null ? nameof(from) : nameof(to));
            if (!_handles.Contains(from) || !_handles.Contains(to))
                throw new ArgumentException("[GraphDialogue] DialogueGraphBuilder.Connect: a node handle does not belong to this builder.");

            _edges.Add(new BaseEdgeData
            {
                Id = Guid.NewGuid().ToString("D"),
                FromNodeId = from.Node.Id,
                ToNodeId = to.Node.Id,
                PortName = string.IsNullOrEmpty(portName) ? "out" : portName
            });
        }

        private void Place(BaseNodeData node)
        {
            node.Id = Guid.NewGuid().ToString("D");
            node.Position = new Vector2(_column++ * 240f, 0f);
            _nodes.Add(node);
        }
    }
}
