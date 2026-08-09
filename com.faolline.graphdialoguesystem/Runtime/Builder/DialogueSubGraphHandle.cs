namespace Faolline.GraphDialogue
{
    /// <summary>
    /// A SubGraph node handle (no type-specific methods beyond the shared wiring surface) for the
    /// node returned by <see cref="DialogueGraphBuilder.AddSubGraph"/> — a dialogue-to-dialogue jump.
    /// </summary>
    public sealed class DialogueSubGraphHandle : DialogueNodeHandle<DialogueSubGraphHandle>
    {
        internal DialogueSubGraphHandle(DialogueGraphBuilder owner, Faolline.GraphCore.BaseNodeData node)
            : base(owner, node) { }
    }
}
