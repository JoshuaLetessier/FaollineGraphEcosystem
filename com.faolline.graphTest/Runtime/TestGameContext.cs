using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Typed context for test graphs. Wraps <see cref="BaseContext"/> string keys
    /// with strongly-typed properties so game-side code has compile-time safety
    /// and IDE autocompletion.
    /// <para>
    /// Conditions and actions still use the underlying string keys (they receive
    /// <see cref="BaseContext"/> and remain generic). This class is the bridge
    /// between game code and the graph — the same pattern a real downstream lib
    /// would follow (e.g., <c>DialogueContext : BaseContext</c>).
    /// </para>
    /// </summary>
    public class TestGameContext : BaseContext
    {
        /// <inheritdoc cref="TestContextKeys.DoorOpen"/>
        public bool DoorOpen
        {
            get => TryGet<bool>(TestContextKeys.DoorOpen, out var v) && v;
            set => Set<bool>(TestContextKeys.DoorOpen, value);
        }

        /// <inheritdoc cref="TestContextKeys.HasItem"/>
        public bool HasItem
        {
            get => TryGet<bool>(TestContextKeys.HasItem, out var v) && v;
            set => Set<bool>(TestContextKeys.HasItem, value);
        }

        /// <inheritdoc cref="TestContextKeys.FlagA"/>
        public bool FlagA
        {
            get => TryGet<bool>(TestContextKeys.FlagA, out var v) && v;
            set => Set<bool>(TestContextKeys.FlagA, value);
        }

        /// <inheritdoc cref="TestContextKeys.FlagB"/>
        public bool FlagB
        {
            get => TryGet<bool>(TestContextKeys.FlagB, out var v) && v;
            set => Set<bool>(TestContextKeys.FlagB, value);
        }

        /// <inheritdoc cref="TestContextKeys.FlagC"/>
        public bool FlagC
        {
            get => TryGet<bool>(TestContextKeys.FlagC, out var v) && v;
            set => Set<bool>(TestContextKeys.FlagC, value);
        }

        protected override BaseContext CreateCloneInstance() => new TestGameContext();
    }
}
