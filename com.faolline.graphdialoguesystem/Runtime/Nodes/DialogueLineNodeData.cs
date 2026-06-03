using System;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// A spoken line in a dialogue: one speaker says one localized line of text. Extends the generic
    /// <see cref="StatementNodeData"/> with a speaker reference, a localized text key, and an optional
    /// expression key. Entry conditions and enter/exit effects are inherited from
    /// <see cref="BaseNodeData"/> (inline reactivity — no condition/effect node types).
    /// </summary>
    [Serializable]
    public class DialogueLineNodeData : StatementNodeData
    {
        /// <summary>Canonical type identifier for dialogue line nodes.</summary>
        public new const string NodeTypeId = "graphdialogue/line";

        [SerializeField] private string _speakerKey = string.Empty;
        [SerializeField] private string _expressionKey = "neutral";

        // The line's localization key is NOT stored as a field — it is derived from this node's Id via
        // DialogueLocalizationKeys.ForLine(node), so it can never be mistyped or drift. The editable
        // Title (inherited from BaseNodeData) is the source text used to pre-fill the table entry.

        /// <summary>Logical speaker id (matches a <see cref="Speaker.SpeakerId"/>). Not translated.</summary>
        public string SpeakerKey
        {
            get => _speakerKey;
            set => _speakerKey = value ?? string.Empty;
        }

        /// <summary>Speaker expression key used to pick a visual expression. Defaults to "neutral".</summary>
        public string ExpressionKey
        {
            get => _expressionKey;
            set => _expressionKey = string.IsNullOrEmpty(value) ? "neutral" : value;
        }
    }
}
