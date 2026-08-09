using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>One validated node within a <see cref="PivotDialogue"/>'s flow.</summary>
    public abstract class PivotDialogueNode
    {
        public string Id { get; }

        protected PivotDialogueNode(string id)
        {
            Id = id;
        }
    }

    public sealed class PivotLine : PivotDialogueNode
    {
        public string SpeakerKey { get; }
        public string Text { get; }
        public string Next { get; }

        public PivotLine(string id, string speakerKey, string text, string next) : base(id)
        {
            SpeakerKey = speakerKey;
            Text = text;
            Next = next;
        }
    }

    public sealed class PivotChoice : PivotDialogueNode
    {
        public IReadOnlyList<PivotChoiceOption> Options { get; }

        public PivotChoice(string id, IReadOnlyList<PivotChoiceOption> options) : base(id)
        {
            Options = options;
        }
    }

    public sealed class PivotChoiceOption
    {
        public string Label { get; }
        public string Next { get; }

        public PivotChoiceOption(string label, string next)
        {
            Label = label;
            Next = next;
        }
    }

    public sealed class PivotEnd : PivotDialogueNode
    {
        /// <summary>Raw reason string (e.g. "Completed") — parsed to graphcore's EndReason Editor-side; see data-model.md.</summary>
        public string Reason { get; }
        public string OutcomeLabel { get; }

        public PivotEnd(string id, string reason, string outcomeLabel) : base(id)
        {
            Reason = reason;
            OutcomeLabel = outcomeLabel;
        }
    }

    public sealed class PivotSubDialogueLink : PivotDialogueNode
    {
        public PivotReference TargetDialogueRef { get; }
        public string Next { get; }

        public PivotSubDialogueLink(string id, PivotReference targetDialogueRef, string next) : base(id)
        {
            TargetDialogueRef = targetDialogueRef;
            Next = next;
        }
    }
}
