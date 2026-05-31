using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Builds a <see cref="NodeExecutorRegistry"/> pre-populated with the dialogue executors.
    /// Centralizes registration so the editor window and the player stay in sync.
    /// </summary>
    public static class DialogueExecutorRegistryFactory
    {
        /// <summary>
        /// Creates a registry with the dialogue line executor registered. Pass the same instance to a
        /// <see cref="BaseRunner"/>. Returns the registry and (optionally) the line executor via
        /// <paramref name="lineExecutor"/> for callers that want to inspect the last entered line.
        /// </summary>
        public static NodeExecutorRegistry Create(out DialogueLineExecutor lineExecutor)
        {
            lineExecutor = new DialogueLineExecutor();
            var registry = new NodeExecutorRegistry();
            registry.Register(lineExecutor);
            return registry;
        }

        /// <summary>Creates a registry with the dialogue line executor registered.</summary>
        public static NodeExecutorRegistry Create() => Create(out _);
    }
}
