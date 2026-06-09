namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Companion keys class for <see cref="GameFlowContext"/> (Constitution VI). Slice 1 stores no domain
    /// parameter keys (the scene name lives on <see cref="LoadSceneAction"/>, the loader is a typed field),
    /// so this is an intentional placeholder. The first domain key a later slice adds is declared here as a
    /// <c>const string</c> — raw key literals never appear at call sites.
    /// </summary>
    public static class GameFlowContextKeys
    {
        // No domain keys in slice 1.
    }
}
