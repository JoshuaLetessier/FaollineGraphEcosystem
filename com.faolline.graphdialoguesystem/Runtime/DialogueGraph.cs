using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Concrete graph asset for an authored dialogue. Owns the dialogue's nodes, edges, parameters,
    /// entry point, a stable <see cref="BaseGraph.GraphId"/> (inherited), and the set of
    /// <see cref="Speaker"/>s used by its line nodes. Opened by
    /// <see cref="Faolline.GraphDialogue.Editor.DialogueGraphEditorWindow"/> and played by
    /// <see cref="DialoguePlayer"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Dialogue Graph", fileName = "NewDialogueGraph")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_graph_dialogue.png")]
    public class DialogueGraph : BaseGraph, ILocalizedGraph
    {
        [SerializeField, Tooltip("Speakers used by this dialogue. Line nodes pick from this list; the " +
            "DialogueDriver reads it so you don't have to assign speakers separately on the scene.")]
        private List<Speaker> _speakers = new List<Speaker>();

        [SerializeField] private GraphLocalizationFlags _localizationFlags = new GraphLocalizationFlags();

        /// <summary>Speakers available to this dialogue's line nodes (assigned in the graph inspector).</summary>
        public IReadOnlyList<Speaker> Speakers => _speakers;

        /// <summary>Inline localization flags (default + per-node overrides). Never null. See <see cref="ILocalizedGraph"/>.</summary>
        public GraphLocalizationFlags LocalizationFlags => _localizationFlags;

        /// <summary>Adds a speaker to the dialogue (no-op for null or duplicates).</summary>
        public void AddSpeaker(Speaker speaker)
        {
            if (speaker != null && !_speakers.Contains(speaker)) _speakers.Add(speaker);
        }

        /// <summary>Removes a speaker from the dialogue. Returns true if it was present.</summary>
        public bool RemoveSpeaker(Speaker speaker) => _speakers.Remove(speaker);

        /// <summary>Finds a speaker by its logical <see cref="Speaker.SpeakerId"/>, or null.</summary>
        public Speaker FindSpeaker(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            foreach (var s in _speakers)
                if (s != null && s.SpeakerId == speakerId) return s;
            return null;
        }
    }
}
