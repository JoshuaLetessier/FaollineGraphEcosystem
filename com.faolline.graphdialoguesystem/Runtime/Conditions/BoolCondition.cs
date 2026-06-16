namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Bool condition. The implementation now lives in <see cref="Faolline.GraphCore.BoolCondition"/> — hoisted to
    /// GraphCore so a consumer using BOTH the GraphDialogue and GraphStandard namespaces no longer hits an
    /// ambiguous-reference (CS0104). This thin subclass is kept so existing <c>GraphDialogue/…</c> assets and code
    /// keep working; new graphs should prefer <see cref="Faolline.GraphCore.BoolCondition"/> directly. (The canonical
    /// reads an absent key as false SILENTLY by default — set <c>WarnOnMissing</c> to restore the old warning.)
    /// </summary>
    public class BoolCondition : Faolline.GraphCore.BoolCondition { }
}
