namespace Faolline.GraphCore
{
    /// <summary>
    /// Graph-asset-aware seeding for <see cref="BaseContext"/>. Split out of Core: <see cref="BaseGraph"/>
    /// and <see cref="VariableDef"/> are ScriptableObject assets, so <see cref="BaseContext"/> itself
    /// cannot name them without breaking its use from a <c>noEngineReferences</c> assembly. These are
    /// extension methods precisely so the call syntax at every existing call site
    /// (<c>context.InitFromGraph(graph)</c>, <c>context.BeginLocalContext(graph)</c>) is unchanged.
    /// </summary>
    public static class BaseContextGraphExtensions
    {
        /// <summary>
        /// Seeds <paramref name="context"/> with the defaults of every <see cref="VariableDef"/>
        /// <paramref name="graph"/> references from its actions/conditions (discovered via
        /// <see cref="GraphVariableScanner"/>). Variables are declaration-free: there is no per-graph
        /// parameter list — the asset carries the type and default, keyed by its stable GUID. A key already
        /// present is left untouched (seed-if-absent), so seeding never clobbers a value the host set
        /// first. A parameter used only from host code is not discovered here and is the host's
        /// responsibility to set (via a <c>GraphVariables</c> constant).
        /// </summary>
        public static void InitFromGraph(this BaseContext context, BaseGraph graph)
            => SeedFromGraph(context, graph, local: false);

        /// <summary>
        /// As <see cref="BaseContext.BeginLocalContext()"/>, then seeds the new local context from the
        /// <see cref="VariableDef"/> defaults <paramref name="seedFrom"/> references (same discovery as
        /// <see cref="InitFromGraph"/>, written into the local overlay). A <c>null</c> graph seeds nothing.
        /// </summary>
        public static void BeginLocalContext(this BaseContext context, BaseGraph seedFrom)
        {
            context.BeginLocalContext();
            if (seedFrom != null)
                SeedFromGraph(context, seedFrom, local: true);
        }

        /// <summary>
        /// Seeds <paramref name="context"/> (global bucket, or the open local overlay when
        /// <paramref name="local"/> is <c>true</c>) from the graph's referenced <see cref="VariableDef"/>
        /// defaults, keyed by GUID, seed-if-absent.
        /// </summary>
        private static void SeedFromGraph(BaseContext context, BaseGraph graph, bool local)
        {
            foreach (var param in GraphVariableScanner.Collect(graph))
            {
                var key = param.Key;
                if (string.IsNullOrEmpty(key)) continue;
                // The default is stored in the field matching the parameter's type — no parsing.
                if (local) context.SeedLocalIfAbsent(key, param.DefaultValueBoxed);
                else context.SeedGlobalIfAbsent(key, param.DefaultValueBoxed);
            }
        }
    }
}
