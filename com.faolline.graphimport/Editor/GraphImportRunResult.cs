namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// The full outcome of a <see cref="GraphImportPipeline.Run"/> call: placement conflicts (never
    /// applied) and generation failures (attempted, but the generator threw). Either one means the
    /// run is not fully clean — a CI script checks <see cref="IsClean"/>, a human reads both lists.
    /// </summary>
    public sealed class GraphImportRunResult
    {
        public ConflictReport Conflicts { get; }
        public ApplyResult Apply { get; }
        public bool IsClean => Conflicts.IsClean && Apply.IsClean;

        public GraphImportRunResult(ConflictReport conflicts, ApplyResult apply)
        {
            Conflicts = conflicts;
            Apply = apply;
        }
    }
}
