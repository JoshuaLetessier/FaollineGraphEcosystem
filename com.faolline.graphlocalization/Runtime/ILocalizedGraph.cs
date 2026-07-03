namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Implemented by a graph asset that carries its localization metadata inline (via an embedded
    /// <see cref="GraphLocalizationFlags"/>), instead of a separate companion asset. The localization editor
    /// tooling (inspector section, auto-builder, per-lib adapters) discovers and reads/writes flags through this
    /// interface, so graphcore stays localization-agnostic and each graph lib opts in with a single serialized
    /// field — the same extension model as <c>DialogueGraph.Speakers</c>.
    /// </summary>
    public interface ILocalizedGraph
    {
        /// <summary>This graph's inline localization flags (default + per-node overrides). Never null.</summary>
        GraphLocalizationFlags LocalizationFlags { get; }
    }
}
