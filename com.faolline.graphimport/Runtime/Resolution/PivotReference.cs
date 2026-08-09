namespace Faolline.GraphImport
{
    /// <summary>A resolved cross-table reference: always the canonical target ID, regardless of whether the source used ID or a name.</summary>
    public sealed class PivotReference
    {
        public string TargetTable { get; }
        public string TargetId { get; }

        public PivotReference(string targetTable, string targetId)
        {
            TargetTable = targetTable;
            TargetId = targetId;
        }
    }
}
