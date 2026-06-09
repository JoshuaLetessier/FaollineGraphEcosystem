using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Typed <see cref="BaseContext"/> for the host layer (Constitution VI). For slice 1 it carries the
    /// active <see cref="ISceneLoader"/> — a runtime service, not a bool/int/float/string parameter, so it
    /// lives as a field rather than a context parameter. This is the single shared blackboard the driver
    /// owns and that later slices (Reactive progression, Flow abilities) will extend.
    /// </summary>
    public class GameFlowContext : BaseContext
    {
        /// <summary>
        /// The scene loader the <see cref="LoadSceneAction"/> uses. The driver sets this at boot (defaulting
        /// to a <see cref="UnitySceneLoader"/>); tests inject a recording stub.
        /// </summary>
        public ISceneLoader SceneLoader { get; set; }

        /// <inheritdoc />
        protected override BaseContext CreateCloneInstance() => new GameFlowContext();

        /// <inheritdoc />
        public override BaseContext DeepClone()
        {
            var clone = (GameFlowContext)base.DeepClone();
            clone.SceneLoader = SceneLoader; // a shared service reference, not per-snapshot state
            return clone;
        }
    }
}
