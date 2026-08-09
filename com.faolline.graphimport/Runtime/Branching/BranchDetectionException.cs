using System;

namespace Faolline.GraphImport
{
    public enum BranchDetectionReason
    {
        MissingOutcome,
        DuplicateOutcome
    }

    /// <summary>
    /// Raised when steps share a position without the declared branch signal cleanly distinguishing
    /// them. Never guessed at, per FR-006 — an unresolvable shared position is always an error.
    /// </summary>
    public sealed class BranchDetectionException : Exception
    {
        public PivotQuest Quest { get; }
        public int Position { get; }
        public BranchDetectionReason Reason { get; }

        public BranchDetectionException(PivotQuest quest, int position, BranchDetectionReason reason)
            : base(BuildMessage(quest, position, reason))
        {
            Quest = quest;
            Position = position;
            Reason = reason;
        }

        static string BuildMessage(PivotQuest quest, int position, BranchDetectionReason reason) =>
            reason == BranchDetectionReason.MissingOutcome
                ? $"Quest '{quest.Id}': multiple steps at position {position} but at least one has no declared branch outcome."
                : $"Quest '{quest.Id}': multiple steps at position {position} declare the same branch outcome.";
    }
}
