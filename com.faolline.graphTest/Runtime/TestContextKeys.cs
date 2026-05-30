namespace Faolline.GraphTest
{
    /// <summary>
    /// Centralized string key constants for <see cref="TestGameContext"/>.
    /// Use these instead of literal strings when setting/reading context values in code,
    /// and when configuring <see cref="TestBoolCondition.ParameterKey"/> or
    /// <see cref="TestSetBoolAction.ParameterKey"/> programmatically.
    /// </summary>
    public static class TestContextKeys
    {
        public const string DoorOpen    = "door_open";
        public const string HasItem     = "has_item";
        public const string FlagA       = "flag_a";
        public const string FlagB       = "flag_b";
        public const string FlagC       = "flag_c";
    }
}
