using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Abstract base for dialogue views. Holds the technology-independent pieces shared by every
    /// front-end: the speaker registry and (added in the avatar story) the avatar lifecycle. Concrete
    /// subclasses implement the actual rendering of lines and choices.
    /// </summary>
    public abstract class DialogueViewBase : MonoBehaviour, IDialogueView
    {
        [Header("Debug")]
        [SerializeField] protected bool verboseLog;

        // Speaker registry, indexed by Speaker.SpeakerId.
        private readonly Dictionary<string, Speaker> _speakersById = new Dictionary<string, Speaker>();

        /// <inheritdoc/>
        public event Action<string> ChoiceSelected;

        /// <summary>Raises <see cref="ChoiceSelected"/> for the given routing id. Used by subclasses.</summary>
        protected void RaiseChoiceSelected(string choiceId)
        {
            if (string.IsNullOrEmpty(choiceId)) return;
            ChoiceSelected?.Invoke(choiceId);
        }

        /// <inheritdoc/>
        public virtual void BindSpeakers(IReadOnlyList<Speaker> speakers)
        {
            _speakersById.Clear();
            if (speakers == null) return;
            foreach (var s in speakers)
            {
                if (s == null || string.IsNullOrEmpty(s.SpeakerId)) continue;
                if (!_speakersById.ContainsKey(s.SpeakerId))
                    _speakersById.Add(s.SpeakerId, s);
            }
        }

        /// <summary>Resolves a bound speaker by id, or null.</summary>
        protected Speaker FindSpeaker(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            return _speakersById.TryGetValue(speakerId, out var s) ? s : null;
        }

        // ── Rendering (implemented by concrete views) ───────────────────────────────

        /// <inheritdoc/>
        public abstract void ShowLine(LineStep step);

        /// <inheritdoc/>
        public abstract void ShowChoices(ChoiceStep step);

        /// <inheritdoc/>
        public abstract void HideAll();
    }
}
