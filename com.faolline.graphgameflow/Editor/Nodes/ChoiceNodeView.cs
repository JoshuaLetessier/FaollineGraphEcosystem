using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Canvas view for a <see cref="ChoiceNodeData"/>. One input "in" and one output port per choice; each
    /// output's <c>portName</c> is the choice's <c>Id</c> (the routing key used by <c>ChooseById</c>) and its
    /// displayed label is the choice's <see cref="BaseChoice.Title"/>. Call <see cref="RebuildPorts"/> after
    /// the choice list changes.
    /// </summary>
    public class ChoiceNodeView : BaseNodeView
    {
        private readonly ChoiceNodeData _data;

        public ChoiceNodeView(ChoiceNodeData data)
        {
            _data = data;
            title = "Choice";
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var input = Port.Create<GameFlowEdgeView>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);

            RebuildPorts();
        }

        /// <summary>Regenerates the output ports from the node's choices (one Single output per choice).</summary>
        public void RebuildPorts()
        {
            outputContainer.Clear();

            if (_data?.Choices != null)
            {
                foreach (var choice in _data.Choices)
                {
                    if (choice == null) continue;

                    var output = Port.Create<GameFlowEdgeView>(
                        Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                    output.portName = choice.Id;   // routing key (GUID)
                    SetPortDisplayLabel(output, ResolveLabel(choice));
                    outputContainer.Add(output);
                }
            }

            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>Updates only the displayed label of the port for <paramref name="choiceId"/> (no rebuild).</summary>
        public void UpdateChoiceLabel(string choiceId, string label)
        {
            foreach (var port in OutputPorts)
            {
                if (port.portName != choiceId) continue;
                var lbl = port.Q<Label>(className: "choice-port-label");
                if (lbl != null) lbl.text = string.IsNullOrEmpty(label) ? choiceId : label;
                return;
            }
        }

        /// <summary>The output ports currently shown, in choice order.</summary>
        public IReadOnlyList<Port> OutputPorts
        {
            get
            {
                var list = new List<Port>();
                foreach (var child in outputContainer.Children())
                    if (child is Port port) list.Add(port);
                return list;
            }
        }

        private static string ResolveLabel(BaseChoice choice)
        {
            if (!string.IsNullOrEmpty(choice.Title)) return choice.Title;
            return string.IsNullOrEmpty(choice.Id) ? "(choice)" : choice.Id;
        }

        // Port.portName IS the connector label text and doubles as the routing key, so we hide the raw
        // connector text (the GUID) and append a separate presentation label for the human-readable title.
        private static void SetPortDisplayLabel(Port port, string text)
        {
            var connectorLabel = port.Q<Label>("type") ?? port.Q<Label>();
            if (connectorLabel != null)
                connectorLabel.style.display = DisplayStyle.None;

            var display = new Label(text);
            display.AddToClassList("choice-port-label");
            port.Add(display);
        }
    }
}
