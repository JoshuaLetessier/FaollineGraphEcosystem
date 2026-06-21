using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Inspector panel for dialogue graphs. Adds the dialogue-specific sections (line speaker/expression, choice,
    /// edge condition, and a Speakers list when nothing is selected) on top of the shared
    /// <see cref="BaseNodeInspectorView"/>, which owns the graph state, the parameter panel, the End/SubGraph
    /// sections, and the universal node section.
    /// </summary>
    public class DialogueNodeInspectorView : BaseNodeInspectorView
    {
        private DialogueGraphView _graphView;

        /// <summary>Provides the canvas view so the inspector can rebuild choice ports and edges.</summary>
        public void SetGraphView(DialogueGraphView graphView) => _graphView = graphView;

        protected override void OnNodeVisualsChanged() => _graphView?.RefreshNodeColors();
        protected override string LogContext => "GraphDialogue";
        protected override string SubGraphSectionTitle => "SubDialogue";

        // ── Node binding ──────────────────────────────────────────────────────
        public override void BindNode(BaseNodeData node)
        {
            // node == null shows the no-selection content (Speakers + Parameters); a real node shows ONLY its
            // sections — so clear without rebuilding the no-selection panel (else they'd stack/overlap).
            if (node == null) { ClearInspector(); return; }
            Clear();

            BoundNode = node;

            // A GraphLink is a non-executing documentary reference → dedicated minimal panel (target + note).
            if (node is GraphLinkNodeData link) { AddGraphLinkSection(link, MarkGraphDirty); return; }

            RefreshSerializedGraph();
            var nodeElement = FindNodeProperty(SerializedGraph, node.Id);

            if (node is DialogueLineNodeData line) BuildLineSection(nodeElement, line);
            if (node is ChoiceNodeData choiceNode) BuildChoiceSection(choiceNode);
            BuildUniversalNodeSections(node);   // End + SubGraph (shared)

            if (nodeElement != null && SerializedGraph != null)
                AddBaseNodeSection(nodeElement, SerializedGraph);
        }

        public override void ClearInspector()
        {
            Clear();
            BoundNode = null;

            if (Graph != null) BuildNoSelectionContent();
        }

        /// <summary>No-selection content for dialogues: the Speakers list above the shared parameter panel.</summary>
        protected override void BuildNoSelectionContent()
        {
            BuildSpeakerPanel();
            BuildParameterPanel();
        }

        // ── Edge binding (condition on a connection) ──────────────────────────
        /// <summary>Binds a selected edge, exposing an ObjectField for its gating condition.</summary>
        public void BindEdge(BaseEdgeData edge)
        {
            Clear();
            BoundNode = null;
            if (edge == null) { ClearInspector(); return; }

            var foldout = new Foldout { text = "Edge", value = true };
            var conditionField = new ObjectField("Condition")
            {
                objectType = typeof(BaseCondition), allowSceneObjects = false, value = edge.Condition
            };
            conditionField.RegisterValueChangedCallback(e => { edge.Condition = e.newValue as BaseCondition; MarkGraphDirty(); });
            foldout.Add(conditionField);
            Add(foldout);
        }

        // ── Line section ──────────────────────────────────────────────────────
        private void BuildLineSection(SerializedProperty nodeElement, DialogueLineNodeData node)
        {
            var foldout = new Foldout { text = "Line", value = true };
            foldout.Add(BuildSpeakerField(node));
            foldout.Add(BuildExpressionField(node));
            Add(foldout);
        }

        // Expression picker: a dropdown sourced from the selected speaker's expressions (never free text).
        private VisualElement BuildExpressionField(DialogueLineNodeData node)
        {
            var dialogueGraph = Graph as DialogueGraph;
            var speaker = dialogueGraph != null ? dialogueGraph.FindSpeaker(node.SpeakerKey) : null;

            var keys = new List<string>();
            if (speaker != null)
                foreach (var e in speaker.Expressions)
                    if (e != null && !string.IsNullOrEmpty(e.Key) && !keys.Contains(e.Key)) keys.Add(e.Key);

            if (!string.IsNullOrEmpty(node.ExpressionKey) && !keys.Contains(node.ExpressionKey))
                keys.Add(node.ExpressionKey);

            if (keys.Count == 0)
            {
                var empty = new DropdownField("Expression", new List<string> { "(define on speaker)" }, 0);
                empty.SetEnabled(false);
                empty.tooltip = speaker == null
                    ? "Select a Speaker first, then add expressions on that Speaker asset."
                    : $"Speaker '{speaker.SpeakerId}' has no expressions — add some on the Speaker asset.";
                return empty;
            }

            var current = string.IsNullOrEmpty(node.ExpressionKey) ? keys[0] : node.ExpressionKey;
            var dropdown = new DropdownField("Expression", keys, Mathf.Max(0, keys.IndexOf(current)));
            dropdown.RegisterValueChangedCallback(e => { node.ExpressionKey = e.newValue; MarkGraphDirty(); });
            return dropdown;
        }

        // Speaker picker: a dropdown of the graph's speaker ids (so the id is never typed by hand).
        private VisualElement BuildSpeakerField(DialogueLineNodeData node)
        {
            const string none = "(none)";
            var dialogueGraph = Graph as DialogueGraph;

            if (dialogueGraph == null || dialogueGraph.Speakers.Count == 0)
            {
                var tf = new TextField("Speaker") { value = node.SpeakerKey };
                tf.RegisterValueChangedCallback(e => { node.SpeakerKey = e.newValue; MarkGraphDirty(); });
                return tf;
            }

            var ids = new List<string> { none };
            foreach (var sp in dialogueGraph.Speakers)
                if (sp != null && !string.IsNullOrEmpty(sp.SpeakerId) && !ids.Contains(sp.SpeakerId))
                    ids.Add(sp.SpeakerId);
            if (!string.IsNullOrEmpty(node.SpeakerKey) && !ids.Contains(node.SpeakerKey))
                ids.Add(node.SpeakerKey);

            var current = string.IsNullOrEmpty(node.SpeakerKey) ? none : node.SpeakerKey;
            var dropdown = new DropdownField("Speaker", ids, ids.IndexOf(current));
            dropdown.RegisterValueChangedCallback(e =>
            {
                node.SpeakerKey = e.newValue == none ? string.Empty : e.newValue;
                MarkGraphDirty();
                RefreshIfBound(node);   // rebuild so the Expression dropdown reflects the new speaker
            });
            return dropdown;
        }

        // ── Choice section (dialogue choice type + canvas ports) ──────────────
        /// <summary>Appends a new <see cref="DialogueChoice"/> and rebuilds the canvas ports.</summary>
        public void AddChoice(ChoiceNodeData node)
        {
            if (node == null) return;
            node.Choices.Add(new DialogueChoice { Id = System.Guid.NewGuid().ToString("D") });
            MarkGraphDirty();
            _graphView?.GetChoiceView(node.Id)?.RebuildPorts();
            _graphView?.ReconnectNodeEdges(node.Id);
            RefreshIfBound(node);
        }

        /// <summary>Removes a choice, deletes its bound edge, rebuilds ports, refreshes inspector.</summary>
        public void RemoveChoice(ChoiceNodeData node, BaseChoice choice)
        {
            if (node == null || choice == null) return;
            if (!node.Choices.Remove(choice)) return;
            _graphView?.RemoveChoiceEdges(node.Id, choice.Id);
            MarkGraphDirty();
            _graphView?.GetChoiceView(node.Id)?.RebuildPorts();
            _graphView?.ReconnectNodeEdges(node.Id);
            RefreshIfBound(node);
        }

        private void BuildChoiceSection(ChoiceNodeData node)
        {
            var foldout = new Foldout { text = "Choices", value = true };

            foreach (var baseChoice in node.Choices)
            {
                if (baseChoice == null) continue;
                var choice = baseChoice;

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                // Friendly name → shown on the port and used as localization source text (key derived from Id).
                var nameField = new TextField("Name") { value = choice.Title };
                nameField.style.flexGrow = 1;
                nameField.RegisterValueChangedCallback(e =>
                {
                    choice.Title = e.newValue;
                    MarkGraphDirty();
                    _graphView?.GetChoiceView(node.Id)?.UpdateChoiceLabel(choice.Id, ChoiceNodeView.ResolveLabel(choice));
                });

                var conditionField = new ObjectField
                {
                    objectType = typeof(BaseCondition), allowSceneObjects = false, value = choice.Condition
                };
                conditionField.style.flexGrow = 1;
                conditionField.RegisterValueChangedCallback(e => { choice.Condition = e.newValue as BaseCondition; MarkGraphDirty(); });

                row.Add(nameField);
                row.Add(conditionField);
                row.Add(new Button(() => RemoveChoice(node, choice)) { text = "×" });
                foldout.Add(row);
            }

            foldout.Add(new Button(() => AddChoice(node)) { text = "Add Choice" });
            Add(foldout);
        }

        // ── Speaker panel (dialogue-specific no-selection section) ─────────────
        private void BuildSpeakerPanel()
        {
            if (!(Graph is DialogueGraph)) return;
            RefreshSerializedGraph();
            if (SerializedGraph == null) return;

            var prop = SerializedGraph.FindProperty("_speakers");
            if (prop == null) return;

            var foldout = new Foldout { text = "Speakers", value = true };
            var field = new PropertyField(prop, "Speakers");
            field.Bind(SerializedGraph);
            foldout.Add(field);
            Add(foldout);
        }
    }
}
