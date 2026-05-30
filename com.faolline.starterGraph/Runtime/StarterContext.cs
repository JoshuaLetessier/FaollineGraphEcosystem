using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Typed context for StarterGraph — the model a downstream lib copies (Constitution Principle VI).
    /// Exposes one strongly-typed property per supported value type (bool/int/float/string), each
    /// going through <see cref="StarterContextKeys"/>, and overrides <see cref="CreateCloneInstance"/>
    /// so history snapshots (GoBack / checkpoints) restore the correct subtype.
    /// <para>
    /// Conditions and actions stay generic (they receive <see cref="BaseContext"/> and use a string
    /// key as serialized data) — only game/editor code touches these typed properties.
    /// Replace the four examples below with your domain's parameters.
    /// </para>
    /// </summary>
    public class StarterContext : BaseContext
    {
        /// <summary>Example bool parameter.</summary>
        public bool Flag
        {
            get => TryGet<bool>(StarterContextKeys.Flag, out var v) && v;
            set => Set<bool>(StarterContextKeys.Flag, value);
        }

        /// <summary>Example int parameter.</summary>
        public int Score
        {
            get => TryGet<int>(StarterContextKeys.Score, out var v) ? v : 0;
            set => Set<int>(StarterContextKeys.Score, value);
        }

        /// <summary>Example float parameter.</summary>
        public float Ratio
        {
            get => TryGet<float>(StarterContextKeys.Ratio, out var v) ? v : 0f;
            set => Set<float>(StarterContextKeys.Ratio, value);
        }

        /// <summary>Example string parameter.</summary>
        public string Label
        {
            get => TryGet<string>(StarterContextKeys.Label, out var v) ? v : string.Empty;
            set => Set<string>(StarterContextKeys.Label, value);
        }

        /// <inheritdoc/>
        protected override BaseContext CreateCloneInstance() => new StarterContext();
    }
}
