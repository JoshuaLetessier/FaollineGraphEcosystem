namespace Faolline.GraphStandard
{
    /// <summary>
    /// Domain-neutral bool condition. The implementation now lives in <see cref="Faolline.GraphCore.BoolCondition"/>
    /// — it was hoisted to GraphCore so a consumer using BOTH the GraphStandard and GraphDialogue namespaces no
    /// longer hits an ambiguous-reference (CS0104). This thin subclass is kept so existing <c>GraphStandard/…</c>
    /// assets and code keep working (it inherits ParameterKey / ExpectedValue / WarnOnMissing and the evaluation);
    /// new graphs should prefer <see cref="Faolline.GraphCore.BoolCondition"/> directly.
    /// </summary>
    public class BoolCondition : Faolline.GraphCore.BoolCondition { }
}
