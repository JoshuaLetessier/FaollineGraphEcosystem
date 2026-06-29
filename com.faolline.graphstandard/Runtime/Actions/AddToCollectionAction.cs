using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>Back-compat subclass — the canonical implementation now lives in
    /// <see cref="Faolline.GraphCore.AddToCollectionAction"/>. Existing assets typed as
    /// <c>Faolline.GraphStandard.AddToCollectionAction</c> keep working; new graphs should
    /// prefer the GraphCore type.</summary>
    public class AddToCollectionAction : GraphCore.AddToCollectionAction { }
}
