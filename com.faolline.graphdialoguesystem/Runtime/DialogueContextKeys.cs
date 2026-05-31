namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Centralized string key constants for <see cref="DialogueContext"/>.
    /// The string literals for context keys live ONLY here (Constitution Principle VI) — code
    /// references the consts, never the raw literals. Replace/extend with your domain keys.
    /// </summary>
    public static class DialogueContextKeys
    {
        public const string Flag    = "flag";     // bool example
        public const string Counter = "counter";  // int example
        public const string Amount  = "amount";   // float example
        public const string Tag     = "tag";      // string example
    }
}
