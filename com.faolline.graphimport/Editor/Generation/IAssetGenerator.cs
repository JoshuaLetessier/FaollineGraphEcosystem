namespace Faolline.GraphImport.Editor
{
    /// <summary>Builds and writes one real asset at <see cref="PlanEntry.ProposedPath"/> from <see cref="PlanEntry.Data"/>.</summary>
    public interface IAssetGenerator
    {
        void Generate(PlanEntry entry);
    }
}
