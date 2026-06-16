using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Canvas view for an <see cref="ObjectiveNodeData"/>. An incoming "requires" port (its prerequisites) and an
    /// outgoing "unlocks" port (objectives it gates) — an edge From→To means "To requires From". Shows the
    /// objective's display label (its Title, or its id).
    /// </summary>
    public sealed class ObjectiveNodeView : BaseNodeView
    {
        private readonly ObjectiveNodeData _data;
        private readonly bool _isEntry;
        private readonly bool _isTerminal;

        public ObjectiveNodeView(ObjectiveNodeData data, bool isEntry = false, bool isTerminal = false)
        {
            _data = data;
            _isEntry = isEntry;
            _isTerminal = isTerminal;
            title = "Objective";
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var input = Port.Create<QuestEdgeView>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = "requires";
            inputContainer.Add(input);

            var output = Port.Create<QuestEdgeView>(
                Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            output.portName = "unlocks";
            outputContainer.Add(output);

            var text = !string.IsNullOrEmpty(_data?.Title) ? _data.Title
                     : !string.IsNullOrEmpty(_data?.Id)    ? _data.Id
                     : "(objective)";
            var label = new Label(text);
            label.AddToClassList("node-label");
            extensionContainer.Add(label);

            // A quest has no Start/End node; instead mark the entry objectives (no prerequisite) and the terminal
            // ones (nothing depends on them) so the begin/end of the DAG reads at a glance. Refreshes on Save / ↻.
            if (_isEntry || _isTerminal)
            {
                var marks = (_isEntry && _isTerminal) ? "▶ entry · ◼ end"
                          : _isEntry ? "▶ entry (no prerequisite)"
                          : "◼ end (nothing depends on it)";
                var tag = new Label(marks);
                tag.style.opacity = 0.6f;
                tag.style.fontSize = 10;
                extensionContainer.Add(tag);
            }

            RefreshExpandedState();
        }
    }
}
