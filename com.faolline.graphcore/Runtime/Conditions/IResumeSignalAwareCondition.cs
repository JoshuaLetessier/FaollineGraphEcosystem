namespace Faolline.GraphCore
{
    /// <summary>
    /// Opt-in refinement for a <see cref="BaseCondition"/> placed on
    /// <see cref="BaseNodeData.ResumeConditions"/>. When a node awaits several signal names at once
    /// (<see cref="BaseNodeData.AwaitSignalNames"/>, logical OR), <see cref="BaseRunner"/> evaluates ALL
    /// <c>ResumeConditions</c> as one AND — regardless of which of those names is the one that actually
    /// fired. A condition that only has an opinion about ONE of the awaited names implements this interface
    /// to receive that name and ABSTAIN (return <c>true</c>) when a different name fired, instead of
    /// vetoing a resume it was never meant to gate.
    /// <para>
    /// This is what makes it possible to combine several single-signal resume conditions on one node — e.g.
    /// one gating a completion signal's payload, another gating a failure signal's payload with a different
    /// match rule — each abstaining on the other's name so together they behave as the intended OR, not an
    /// accidental AND. <see cref="BaseCondition"/> implementers that don't need this stay untouched; the
    /// runner falls back to the plain <see cref="BaseCondition.Evaluate"/> for them.
    /// </para>
    /// </summary>
    public interface IResumeSignalAwareCondition
    {
        /// <summary>
        /// As <see cref="BaseCondition.Evaluate"/>, but also told which awaited signal name is the one that
        /// just fired (null when the caller cannot determine a single triggering name — treat as "evaluate
        /// normally", the same as <see cref="BaseCondition.Evaluate"/> would).
        /// </summary>
        bool EvaluateResume(BaseContext context, string raisedSignalName);
    }
}
