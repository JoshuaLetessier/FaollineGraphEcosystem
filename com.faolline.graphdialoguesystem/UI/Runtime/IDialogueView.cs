using System;
using System.Collections.Generic;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Presentation boundary the <see cref="DialogueDriver"/> talks to. A view renders the player's
    /// already-resolved steps and reports the player's choice selection. Implementations perform NO
    /// localization (the player resolves text upstream) — they display the resolved strings verbatim.
    /// Swapping UI technology means swapping the view implementation only.
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>Supplies the speakers used to resolve avatars (indexed by <see cref="Speaker.SpeakerId"/>).</summary>
        void BindSpeakers(IReadOnlyList<Speaker> speakers);

        /// <summary>Renders a spoken line: resolved text + resolved speaker name, and requests the matching avatar.</summary>
        void ShowLine(LineStep step);

        /// <summary>Renders the choice options: one control per option; unavailable options are non-selectable.</summary>
        void ShowChoices(ChoiceStep step);

        /// <summary>Clears all displayed text, choices, and avatars.</summary>
        void HideAll();

        /// <summary>Raised when the player selects an option; carries the option's <c>ChoiceId</c> (routing key).</summary>
        event Action<string> ChoiceSelected;
    }
}
