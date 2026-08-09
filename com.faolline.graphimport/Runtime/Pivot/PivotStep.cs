namespace Faolline.GraphImport
{
    /// <summary>A single position within a quest's flow, referencing its content rather than containing it.</summary>
    public sealed class PivotStep
    {
        public string Id { get; }
        public PivotQuest Quest { get; }
        public int Order { get; }
        public PivotReference ContentRef { get; }
        public string BranchOutcome { get; }

        public PivotStep(string id, PivotQuest quest, int order, PivotReference contentRef, string branchOutcome)
        {
            Id = id;
            Quest = quest;
            Order = order;
            ContentRef = contentRef;
            BranchOutcome = branchOutcome;
        }
    }
}
