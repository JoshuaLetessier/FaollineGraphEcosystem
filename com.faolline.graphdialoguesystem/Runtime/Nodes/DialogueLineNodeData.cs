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
        [SerializeField] private string _textKey = string.Empty;
        [SerializeField] private string _expressionKey = "neutral";

        // Note: the node Title (editable display name, also used as source text to pre-fill this line's
        // localization entry) is inherited from BaseNodeData.Title.

        /// <summary>Logical speaker id (matches a <see cref="Speaker.SpeakerId"/>). Not translated.</summary>
        public string SpeakerKey
        {
            get => _speakerKey;
            set => _speakerKey = value ?? string.Empty;
        }

        /// <summary>Localization key for this line's spoken text.</summary>
        public string TextKey
        {
            get => _textKey;
            set => _textKey = value ?? string.Empty;
        }

        /// <summary>Speaker expression key used to pick a visual expression. Defaults to "neutral".</summary>
        public string ExpressionKey
        {
            get => _expressionKey;
            set => _expressionKey = string.IsNullOrEmpty(value) ? "neutral" : value;
        }
    }
}
