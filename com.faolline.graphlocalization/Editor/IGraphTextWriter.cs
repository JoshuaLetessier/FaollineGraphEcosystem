namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Implemented by a lib's localization adapter to copy a table text back into the graph text it was seeded
    /// from (see <see cref="GraphTextSync.Apply"/>). <see cref="BaseGraphLocalizationAdapter{TGraph}"/> implements it
    /// and exposes two hooks; implement it directly only for an adapter that does not derive from that base.
    /// </summary>
    public interface IGraphTextWriter
    {
        /// <summary>
        /// Writes <see cref="GraphTextDrift.TableText"/> into the graph text identified by <paramref name="drift"/>
        /// (recording Undo and marking the asset dirty; saving is the caller's job). Returns false when the text
        /// could not be found or written.
        /// </summary>
        bool WriteGraphText(GraphTextDrift drift);
    }
}
