using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>Back-compat subclass — the canonical implementation now lives in
    /// <see cref="Faolline.GraphCore.RemoveFromCollectionAction"/>. Existing assets typed as
    /// <c>Faolline.GraphStandard.RemoveFromCollectionAction</c> keep working; new graphs should
    /// prefer the GraphCore type.</summary>
    public class RemoveFromCollectionAction : GraphCore.RemoveFromCollectionAction { }
}
