namespace Faolline.GraphDialogue
{
    /// <summary>
    /// SetFloatAction. The implementation now lives in <see cref="Faolline.GraphCore.SetFloatAction"/> — hoisted to GraphCore so a
    /// consumer using both the GraphDialogue and GraphStandard namespaces no longer hits an ambiguous-reference
    /// (CS0104). Thin back-compat subclass: existing <c>GraphDialogue/…</c> assets and code keep working; new graphs
    /// should prefer <see cref="Faolline.GraphCore.SetFloatAction"/> directly.
    /// </summary>
    public class SetFloatAction : Faolline.GraphCore.SetFloatAction { }
}
