using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Scene-to-graph bridge: executes a list of <see cref="BaseAction"/>s against the active
    /// <see cref="GraphFlowDriver"/>'s context when <see cref="Fire"/> is called — manually, or
    /// automatically via built-in physics triggers (<see cref="AutoMode"/>).
    /// <para>
    /// An optional <see cref="BaseCondition"/> guard can gate the trigger: <see cref="Fire"/> only
    /// proceeds when the guard evaluates true against the active context (e.g. "player has key").
    /// </para>
    /// </summary>
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/Assets/FaollineGraphEcosystem/com.faolline.graphgameflow/README.md")]
    public class ContextTrigger : MonoBehaviour
    {
        public enum TriggerMode
        {
            Manual,
            OnTriggerEnter,
            OnTriggerExit,
            OnCollisionEnter
        }

        [Header("Trigger")]
        [Tooltip("Manual = call Fire() from code/UnityEvent. Others fire automatically on physics events.")]
        [SerializeField] private TriggerMode _autoMode = TriggerMode.Manual;

        [Tooltip("Optional tag filter for physics triggers (empty = any object triggers).")]
        [TagSelector]
        [SerializeField] private string _requiredTag;

        [Header("Guard")]
        [Tooltip("Optional condition that must pass before actions execute. Null = no guard.")]
        [SerializeField] private BaseCondition _guard;

        [Header("Actions")]
        [Tooltip("Actions executed on the active context when fired.")]
        [SerializeField] private List<BaseAction> _actions = new List<BaseAction>();

        [Tooltip("Optional signal raised on the context after actions execute. Use a SignalName asset for safety, or type a raw string.")]
        [SerializeField] private SignalName _signalAsset;
        [SerializeField] private string _signalRaw;

        [Header("Options")]
        [Tooltip("When true, Fire() does nothing after the first successful invocation.")]
        [SerializeField] private bool _fireOnce = true;

        [Tooltip("Optional GameObjects to activate when fired (e.g. a puzzle prefab, a VFX).")]
        [SerializeField] private List<GameObject> _activate = new List<GameObject>();

        [Tooltip("Optional GameObjects to deactivate when fired (e.g. hide the interactable).")]
        [SerializeField] private List<GameObject> _deactivate = new List<GameObject>();

        private bool _fired;

        /// <summary>True after the first successful <see cref="Fire"/> (when fire-once is on).</summary>
        public bool HasFired => _fired;

        /// <summary>
        /// Executes all configured actions against the active driver's context, toggles GameObjects,
        /// and optionally raises a signal. Respects the guard condition and fire-once flag.
        /// </summary>
        public void Fire()
        {
            if (_fireOnce && _fired) return;

            var driver = GraphFlowDriver.Active;
            if (driver == null || driver.Context == null)
            {
                Debug.LogWarning($"[GraphGameFlow] ContextTrigger '{name}': no active GraphFlowDriver; ignored.");
                return;
            }

            var ctx = driver.Context;

            if (_guard != null && !_guard.Evaluate(ctx))
                return;

            _fired = true;

            foreach (var action in _actions)
                if (action != null) action.Execute(ctx);

            foreach (var go in _activate)
                if (go != null) go.SetActive(true);

            foreach (var go in _deactivate)
                if (go != null) go.SetActive(false);

            string signal = _signalAsset != null ? (string)_signalAsset : _signalRaw;
            if (!string.IsNullOrEmpty(signal))
                ctx.RaiseSignal(signal);
        }

        /// <summary>Resets the fire-once guard so the trigger can fire again.</summary>
        public void ResetTrigger() => _fired = false;

        // ── Physics auto-triggers ────────────────────────────────────────────

        private bool PassesTagFilter(GameObject other)
            => string.IsNullOrEmpty(_requiredTag) || other.CompareTag(_requiredTag);

        private void OnTriggerEnter(Collider other)
        {
            if (_autoMode == TriggerMode.OnTriggerEnter && PassesTagFilter(other.gameObject))
                Fire();
        }

        private void OnTriggerExit(Collider other)
        {
            if (_autoMode == TriggerMode.OnTriggerExit && PassesTagFilter(other.gameObject))
                Fire();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_autoMode == TriggerMode.OnCollisionEnter && PassesTagFilter(collision.gameObject))
                Fire();
        }
    }
}
