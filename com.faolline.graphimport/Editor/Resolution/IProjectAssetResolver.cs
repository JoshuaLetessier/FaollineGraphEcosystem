using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// The single seam <see cref="DialogueAssetGenerator"/> uses to find an existing project asset
    /// from an external identifier (sub-dialogue refs, speaker refs).
    /// </summary>
    public interface IProjectAssetResolver
    {
        /// <summary>Resolves a sub-dialogue ref to its graph asset, or null if not found.</summary>
        BaseGraph ResolveGraph(string targetTable, string targetId);

        /// <summary>Resolves a speaker key to its <see cref="Speaker"/> asset, or null if not found.</summary>
        Speaker ResolveSpeaker(string speakerKey);
    }

    /// <summary>
    /// V1's only implementation: both methods return null. Matches the existing precedent (a null
    /// SubGraphNodeData.TargetGraph is graphcore's own documented "incomplete node" state, never a
    /// crash) — a real disk-lookup implementation is out of scope for now.
    /// </summary>
    public sealed class NullProjectAssetResolver : IProjectAssetResolver
    {
        public BaseGraph ResolveGraph(string targetTable, string targetId) => null;
        public Speaker ResolveSpeaker(string speakerKey) => null;
    }
}
