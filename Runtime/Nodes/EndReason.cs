namespace Faolline.GraphCore
{
    /// <summary>Describes why a graph execution reached an <see cref="EndNodeData"/>.</summary>
    public enum EndReason
    {
        Completed = 0,
        Cancelled = 1,
        Error     = 2
    }
}
