namespace Faolline.GraphImport
{
    /// <summary>Derives an asset's proposed location from a configurable rule per asset kind.</summary>
    public interface IPathTemplateResolver
    {
        string Resolve(PlanEntryKind kind, PivotQuest quest);
    }
}
