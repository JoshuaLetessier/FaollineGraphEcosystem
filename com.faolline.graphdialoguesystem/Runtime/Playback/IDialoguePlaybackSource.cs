using System;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Runner-agnostic source of dialogue steps: anything that can emit <see cref="LineStep"/>/
    /// <see cref="ChoiceStep"/>/<see cref="EndStep"/> and accept <see cref="Advance"/>/<see cref="Choose"/>.
    /// <see cref="DialoguePlayer"/> implements this directly for standalone playback; a flow-embedded
    /// dialogue (driven by a host runner) implements it via a presenter-backed adapter instead.
    /// </summary>
    public interface IDialoguePlaybackSource
    {
        /// <summary>Raised when a spoken line is ready.</summary>
        event Action<LineStep> OnLine;

        /// <summary>Raised when a choice point is ready.</summary>
        event Action<ChoiceStep> OnChoices;

        /// <summary>Raised when the dialogue (or dialogue segment) ends.</summary>
        event Action<EndStep> OnEnded;

        /// <summary>Raised when no valid branch is available (stuck).</summary>
        event Action OnStuck;

        /// <summary>Advances past the current line.</summary>
        void Advance();

        /// <summary>Selects the choice with <paramref name="choiceId"/>.</summary>
        void Choose(string choiceId);
    }
}
