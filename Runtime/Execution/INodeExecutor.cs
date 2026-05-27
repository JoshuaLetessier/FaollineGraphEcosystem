namespace Faolline.GraphCore
{
    /// <summary>
    /// Pluggable execution handler for a specific node type. Implement and register
    /// with <see cref="NodeExecutorRegistry"/> to provide type-specific runtime behaviour
    /// without modifying graphcore. <see cref="Undo"/> defaults to a no-op; override when
    /// the executor has reversible side-effects.
    /// </summary>
    public interface INodeExecutor
    {
        /// <summary>
        /// The node type string this executor handles. MUST match a <c>NodeTypeId</c> const
        /// on the corresponding node class (e.g., <c>StatementNodeData.NodeTypeId</c>).
        /// </summary>
        string NodeType { get; }

        /// <summary>Executes this node's type-specific logic.</summary>
        void Execute(BaseNodeData node, BaseContext context);

        /// <summary>
        /// Reverses the side-effects of the most recent <see cref="Execute"/> call.
        /// Default implementation is a no-op. Override for executors with reversible effects.
        /// </summary>
        void Undo(BaseNodeData node, BaseContext context) { }
    }
}
