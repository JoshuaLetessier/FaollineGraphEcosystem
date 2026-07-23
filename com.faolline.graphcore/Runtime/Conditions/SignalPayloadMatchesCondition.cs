using System;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>How <see cref="SignalPayloadMatchesCondition"/> compares a payload to its expected value.</summary>
    public enum SignalPayloadMatchMode
    {
        /// <summary>Payload must equal <see cref="SignalPayloadMatchesCondition.ExpectedValue"/> exactly.</summary>
        Exact,
        /// <summary>Payload must start with <see cref="SignalPayloadMatchesCondition.ExpectedValue"/> — for a
        /// signal whose payload embeds extra detail after a common prefix (e.g. a failure signal's
        /// <c>"{name}: {reason}"</c> format).</summary>
        StartsWith
    }

    /// <summary>
    /// Returns <c>true</c> when the named signal's LAST raised string payload matches
    /// <see cref="ExpectedValue"/> (per <see cref="MatchMode"/>). Built for disambiguating a shared signal
    /// name raised by several independent sources with different payloads (e.g. a scene-loader's completion
    /// signal shared across multiple concurrently-parked flows, where the payload carries the scene name) —
    /// pair with <see cref="BaseNodeData.ResumeConditions"/> so an awaiting node only resumes on the raise
    /// that was actually meant for it, instead of on any raise of that signal name.
    /// <para>
    /// A non-string payload (or no payload at all) never matches — this condition only compares strings.
    /// </para>
    /// <para>
    /// For a fixed, small set of named cases, create one asset per case and set <see cref="ExpectedValue"/>
    /// in the Inspector. For a dynamic/procedural set (e.g. streamed world tiles whose names aren't known at
    /// authoring time), create an instance at runtime with <c>ScriptableObject.CreateInstance</c> and set
    /// <see cref="ExpectedValue"/> from code at the same point the corresponding load is issued — the same
    /// per-instance wiring already needed for that load's own target name.
    /// </para>
    /// <para>
    /// <b>Awaiting more than one signal name</b> (e.g. a completion signal plus a failure signal added via
    /// <see cref="BaseNodeData.AwaitSignalNamesExtra"/>): put one instance of this condition PER awaited
    /// name in <see cref="BaseNodeData.ResumeConditions"/>, each targeting its own <see cref="Signal"/>.
    /// This class implements <see cref="IResumeSignalAwareCondition"/>, so each instance abstains (passes)
    /// on any raise that isn't its own <see cref="Signal"/> instead of vetoing it — together they behave as
    /// the intended OR rather than an accidental AND. Typical pairing for the completion/failure case: one
    /// instance on the completion signal with <see cref="SignalPayloadMatchMode.Exact"/>, another on the
    /// failure signal with <see cref="SignalPayloadMatchMode.StartsWith"/> (failure payloads carry
    /// <c>"{name}: {reason}"</c>, e.g. from <c>AsyncSceneLoader</c>/<c>AddressablesSceneLoader</c>).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Signal Payload Matches", fileName = "SignalPayloadMatchesCondition")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_condition.png")]
    public class SignalPayloadMatchesCondition : BaseCondition, IResumeSignalAwareCondition
    {
        [SerializeField] private SignalDef _signal;
        [SerializeField] private string _expectedValue;
        [SerializeField] private SignalPayloadMatchMode _matchMode = SignalPayloadMatchMode.Exact;

        public SignalDef Signal { get => _signal; set => _signal = value; }
        public string ExpectedValue { get => _expectedValue; set => _expectedValue = value; }
        public SignalPayloadMatchMode MatchMode { get => _matchMode; set => _matchMode = value; }

        /// <summary>Evaluates as if this condition's own <see cref="Signal"/> were the one that fired.</summary>
        public override bool Evaluate(BaseContext context)
            => EvaluateResume(context, _signal != null ? (string)_signal : null);

        public bool EvaluateResume(BaseContext context, string raisedSignalName)
        {
            if (context == null || _signal == null) return false;
            string name = (string)_signal;
            if (string.IsNullOrEmpty(name)) return false;

            // A different awaited name triggered this resume — this condition has no opinion on it.
            if (raisedSignalName != null && raisedSignalName != name) return true;

            if (!context.TryGetLastSignal(name, out var args) || !args.HasPayload) return false;
            if (!(args.PayloadBoxed is string value)) return false;

            return _matchMode == SignalPayloadMatchMode.StartsWith
                ? value.StartsWith(_expectedValue ?? string.Empty, StringComparison.Ordinal)
                : value == _expectedValue;
        }
    }
}
