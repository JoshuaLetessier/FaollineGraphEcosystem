namespace Faolline.GraphStandard
{
    /// <summary>
    /// Domain-neutral bool setter. The implementation now lives in <see cref="Faolline.GraphCore.SetBoolAction"/>
    /// — hoisted to GraphCore so a consumer using BOTH the GraphStandard and GraphDialogue namespaces no longer
    /// hits an ambiguous-reference (CS0104). This thin subclass is kept so existing <c>GraphStandard/…</c> assets
    /// and code keep working; new graphs should prefer <see cref="Faolline.GraphCore.SetBoolAction"/> directly.
    /// </summary>
    public class SetBoolAction : Faolline.GraphCore.SetBoolAction { }
}
