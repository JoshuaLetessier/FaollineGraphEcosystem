namespace Faolline.GraphStandard
{
    /// <summary>
    /// Domain-neutral SetFloatAction. The implementation now lives in <see cref="Faolline.GraphCore.SetFloatAction"/> — hoisted to
    /// GraphCore so a consumer using both the GraphStandard and GraphDialogue namespaces no longer hits an
    /// ambiguous-reference (CS0104). Thin back-compat subclass: existing <c>GraphStandard/…</c> assets and code keep
    /// working; new graphs should prefer <see cref="Faolline.GraphCore.SetFloatAction"/> directly.
    /// </summary>
    public class SetFloatAction : Faolline.GraphCore.SetFloatAction { }
}
