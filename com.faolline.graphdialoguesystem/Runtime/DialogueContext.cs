using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Typed context for the dialogue system — the runtime blackboard read by conditions and written
    /// by effects (Constitution Principle VI). Exposes one strongly-typed property per supported value
    /// type (bool/int/float/string), each going through <see cref="DialogueContextKeys"/>, and overrides
    /// <see cref="CreateCloneInstance"/> so history snapshots (GoBack / checkpoints) restore the correct
    /// subtype.
    /// <para>
    /// Conditions and actions stay generic (they receive <see cref="BaseContext"/> and use a string key
    /// as serialized data) — only game/editor code touches these typed properties. Replace the four
    /// examples below with your dialogue's parameters.
    /// </para>
    /// </summary>
    public class DialogueContext : BaseContext
    {
        /// <summary>Example bool parameter.</summary>
        public bool Flag
        {
            get => TryGet<bool>(DialogueContextKeys.Flag, out var v) && v;
            set => Set<bool>(DialogueContextKeys.Flag, value);
        }

        /// <summary>Example int parameter.</summary>
        public int Counter
        {
            get => TryGet<int>(DialogueContextKeys.Counter, out var v) ? v : 0;
            set => Set<int>(DialogueContextKeys.Counter, value);
        }

        /// <summary>Example float parameter.</summary>
        public float Amount
        {
            get => TryGet<float>(DialogueContextKeys.Amount, out var v) ? v : 0f;
            set => Set<float>(DialogueContextKeys.Amount, value);
        }

        /// <summary>Example string parameter.</summary>
        public string Tag
        {
            get => TryGet<string>(DialogueContextKeys.Tag, out var v) ? v : string.Empty;
            set => Set<string>(DialogueContextKeys.Tag, value);
        }

        /// <inheritdoc/>
        protected override BaseContext CreateCloneInstance() => new DialogueContext();
    }
}
