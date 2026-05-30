using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Canvas view for <see cref="ChoiceNodeData"/>.
    /// One input port "in" and one output port per choice. Each output port's
    /// <c>portName</c> is the choice's <c>Id</c> (used for edge routing via
    /// <c>ChooseById</c>); its displayed label is the choice's <see cref="TestChoice.Label"/>.
    /// Call <see cref="RebuildPorts"/> after the choice list changes to re-derive the ports.
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
            var input = Port.Create<TestEdgeView>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);

            RebuildPorts();
        }

        /// <summary>
        /// Clears and regenerates the output ports from the node's <see cref="ChoiceNodeData.Choices"/>.
        /// One <see cref="Port.Capacity.Single"/> output per choice; <c>portName = choice.Id</c>,
        /// displayed connector label = the choice's <see cref="TestChoice.Label"/>.
        /// </summary>
        public void RebuildPorts()
        {
            outputContainer.Clear();

            if (_data?.Choices != null)
            {
                foreach (var choice in _data.Choices)
                {
                    if (choice == null) continue;

                    var output = Port.Create<TestEdgeView>(
                        Orientation.Horizontal,
                        Direction.Output,
                        Port.Capacity.Single,
                        typeof(bool));

                    // portName is the routing key (the choice GUID); the displayed label is presentation only.
                    output.portName = choice.Id;
                    SetPortDisplayLabel(output, ResolveLabel(choice));
                    outputContainer.Add(output);
                }
            }

            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>
        /// Updates just the displayed label of the output port whose <c>portName</c> matches
        /// <paramref name="choiceId"/>, without recreating ports (so connected edges stay intact).
        /// </summary>
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

        /// <summary>The output ports currently shown, in choice order. Exposed for tests/inspection.</summary>
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
            if (choice is TestChoice tc && !string.IsNullOrEmpty(tc.Label))
                return tc.Label;
            return string.IsNullOrEmpty(choice.Id) ? "(choice)" : choice.Id;
        }

        // In GraphView, Port.portName IS the connector label's text — so we must NOT overwrite it,
        // or the routing key (the choice GUID) would change. Instead hide the raw connector text
        // (the GUID) and append a separate presentation label showing the human-readable choice text.
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
