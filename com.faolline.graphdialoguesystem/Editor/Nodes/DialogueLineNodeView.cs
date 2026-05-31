using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Canvas view for <see cref="DialogueLineNodeData"/>. One input "in" and one output "out".
    /// Shows the speaker key and the line's text key in the node body.
    /// </summary>
    public class DialogueLineNodeView : BaseNodeView
    {
        private readonly DialogueLineNodeData _data;

        public DialogueLineNodeView(DialogueLineNodeData data)
        {
            _data = data;
            title = "Line";
            AddToClassList("gd-node-line");
            Initialize(data);
        }

        protected override void OnBuildView()
        {
            var input = Port.Create<DialogueEdgeView>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = "in";
            inputContainer.Add(input);

            var output = Port.Create<DialogueEdgeView>(
                Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            output.portName = "out";
            outputContainer.Add(output);

            var speaker = new Label(string.IsNullOrEmpty(_data?.SpeakerKey) ? "(no speaker)" : _data.SpeakerKey);
            speaker.AddToClassList("gd-node-speaker");
            extensionContainer.Add(speaker);

            var text = new Label(string.IsNullOrEmpty(_data?.TextKey) ? "(no text key)" : _data.TextKey);
            text.AddToClassList("node-label");
            extensionContainer.Add(text);

            RefreshExpandedState();
        }
    }
}
