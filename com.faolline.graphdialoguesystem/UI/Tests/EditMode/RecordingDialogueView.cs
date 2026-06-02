using System;
using System.Collections.Generic;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>Test double: an <see cref="IDialogueView"/> that records calls and can raise selection.</summary>
    public sealed class RecordingDialogueView : IDialogueView
    {
        public LineStep LastLine;
        public ChoiceStep LastChoices;
        public int ShowLineCount;
        public int ShowChoicesCount;
        public int HideAllCount;
        public IReadOnlyList<Speaker> BoundSpeakers;

        public event Action<string> ChoiceSelected;

        public void BindSpeakers(IReadOnlyList<Speaker> speakers) => BoundSpeakers = speakers;

        public void ShowLine(LineStep step)
        {
            LastLine = step; LastChoices = null; ShowLineCount++;
        }

        public void ShowChoices(ChoiceStep step)
        {
            LastChoices = step; LastLine = null; ShowChoicesCount++;
        }

        public void HideAll() => HideAllCount++;

        /// <summary>Simulates the player selecting an option (as a real view would on click).</summary>
        public void RaiseChoiceSelected(string choiceId) => ChoiceSelected?.Invoke(choiceId);
    }
}
