using System;
using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Maps <see cref="INodeExecutor.NodeType"/> strings to their executor instances.
    /// Register downstream executors here; <see cref="BaseRunner"/> uses this registry
    /// to dispatch type-specific execution without any graphcore modification.
    /// </summary>
    public class NodeExecutorRegistry
    {
        private readonly Dictionary<string, INodeExecutor> _executors =
            new Dictionary<string, INodeExecutor>();

        /// <summary>
        /// Registers an executor for <see cref="INodeExecutor.NodeType"/>.
        /// Silently replaces any previously registered executor for the same type.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="executor"/>'s <c>NodeType</c> is <c>null</c>.
        /// </exception>
        public void Register(INodeExecutor executor)
        {
            if (executor.NodeType == null)
                throw new ArgumentNullException(nameof(executor),
                    "[GraphCore] INodeExecutor.NodeType must not be null.");
            _executors[executor.NodeType] = executor;
        }

        /// <summary>
        /// Returns the executor registered for <paramref name="nodeType"/>,
        /// or <c>null</c> if no executor is registered for that type.
        /// </summary>
        public INodeExecutor GetExecutor(string nodeType)
        {
            _executors.TryGetValue(nodeType, out var executor);
            return executor;
        }
    }
}
