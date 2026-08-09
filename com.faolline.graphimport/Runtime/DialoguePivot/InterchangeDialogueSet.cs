using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Faolline.GraphImport
{
    /// <summary>Which shape an <see cref="InterchangeNode"/> carries — only the fields for that kind are meaningful.</summary>
    public enum InterchangeNodeKind
    {
        Line,
        Choice,
        End,
        SubDialogue
    }

    /// <summary>
    /// Raw, unvalidated deserialization of one interchange file — 1:1 with the JSON. Structural
    /// correctness (dangling refs, duplicate ids, cycles) is <see cref="DialoguePivotBuilder"/>'s job,
    /// not this type's.
    /// </summary>
    public sealed class InterchangeDialogueSet
    {
        public IReadOnlyList<InterchangeDialogue> Dialogues { get; }

        public InterchangeDialogueSet(IReadOnlyList<InterchangeDialogue> dialogues)
        {
            Dialogues = dialogues;
        }

        public static InterchangeDialogueSet LoadFromJson(string json)
        {
            var root = JObject.Parse(json);
            var dialogues = ((JArray)root["dialogues"]).Select(ParseDialogue).ToList();
            return new InterchangeDialogueSet(dialogues);
        }

        static InterchangeDialogue ParseDialogue(JToken token)
        {
            var d = (JObject)token;
            var nodes = ((JArray)d["nodes"]).Select(ParseNode).ToList();
            return new InterchangeDialogue((string)d["id"], (string)d["name"], (string)d["entryNodeId"], nodes);
        }

        static InterchangeNode ParseNode(JToken token)
        {
            var n = (JObject)token;
            var kind = ParseKind((string)n["kind"]);
            var options = (n["options"] as JArray)?.Select(o => new InterchangeChoiceOption(
                (string)o["label"], (string)o["next"]
            )).ToList();

            return new InterchangeNode(
                id: (string)n["id"],
                kind: kind,
                speakerKey: (string)n["speakerKey"],
                text: (string)n["text"],
                next: (string)n["next"],
                options: options,
                reason: (string)n["reason"],
                outcomeLabel: (string)n["outcomeLabel"],
                targetDialogue: (string)n["targetDialogue"]
            );
        }

        static InterchangeNodeKind ParseKind(string value)
        {
            switch (value)
            {
                case "line": return InterchangeNodeKind.Line;
                case "choice": return InterchangeNodeKind.Choice;
                case "end": return InterchangeNodeKind.End;
                case "subDialogue": return InterchangeNodeKind.SubDialogue;
                default: throw new System.InvalidOperationException($"Unknown interchange node kind '{value}'.");
            }
        }
    }

    public sealed class InterchangeDialogue
    {
        public string Id { get; }
        public string Name { get; }
        public string EntryNodeId { get; }
        public IReadOnlyList<InterchangeNode> Nodes { get; }

        public InterchangeDialogue(string id, string name, string entryNodeId, IReadOnlyList<InterchangeNode> nodes)
        {
            Id = id;
            Name = name;
            EntryNodeId = entryNodeId;
            Nodes = nodes;
        }
    }

    /// <summary>One raw node — only the fields relevant to <see cref="Kind"/> are populated.</summary>
    public sealed class InterchangeNode
    {
        public string Id { get; }
        public InterchangeNodeKind Kind { get; }

        // line
        public string SpeakerKey { get; }
        public string Text { get; }
        public string Next { get; }

        // choice
        public IReadOnlyList<InterchangeChoiceOption> Options { get; }

        // end
        public string Reason { get; }
        public string OutcomeLabel { get; }

        // subDialogue
        public string TargetDialogue { get; }

        public InterchangeNode(string id, InterchangeNodeKind kind, string speakerKey, string text, string next,
            IReadOnlyList<InterchangeChoiceOption> options, string reason, string outcomeLabel, string targetDialogue)
        {
            Id = id;
            Kind = kind;
            SpeakerKey = speakerKey;
            Text = text;
            Next = next;
            Options = options;
            Reason = reason;
            OutcomeLabel = outcomeLabel;
            TargetDialogue = targetDialogue;
        }
    }

    public sealed class InterchangeChoiceOption
    {
        public string Label { get; }
        public string Next { get; }

        public InterchangeChoiceOption(string label, string next)
        {
            Label = label;
            Next = next;
        }
    }
}
