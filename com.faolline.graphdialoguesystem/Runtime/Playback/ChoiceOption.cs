namespace Faolline.GraphDialogue
{
    /// <summary>
    /// One presented choice option: its routing id, its localized label, and whether it is currently
    /// selectable (its condition passes against the live context).
    /// </summary>
    public sealed class ChoiceOption
    {
        /// <summary>Routing id (the choice's stable Id) passed to <see cref="DialoguePlayer.Choose"/>.</summary>
        public string ChoiceId { get; }

        /// <summary>Choice label resolved into the active locale (or a fallback).</summary>
        public string ResolvedLabel { get; }

        /// <summary>True when this option's condition passes (or it has none).</summary>
        public bool Available { get; }

        public ChoiceOption(string choiceId, string resolvedLabel, bool available)
        {
            ChoiceId = choiceId ?? string.Empty;
            ResolvedLabel = resolvedLabel ?? string.Empty;
            Available = available;
        }
    }
}
