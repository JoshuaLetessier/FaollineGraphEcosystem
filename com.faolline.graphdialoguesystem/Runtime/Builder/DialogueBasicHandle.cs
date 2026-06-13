namespace Faolline.GraphDialogue
{
    /// <summary>
    /// A plain node handle (no type-specific methods) for nodes that only need the shared wiring surface —
    /// e.g. the End node returned by <see cref="DialogueGraphBuilder.AddEnd"/>.
    /// </summary>
    public sealed class DialogueBasicHandle : DialogueNodeHandle<DialogueBasicHandle>
    {
        internal DialogueBasicHandle(DialogueGraphBuilder owner, Faolline.GraphCore.BaseNodeData node)
            : base(owner, node) { }
    }
}
