using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Scene-to-graph bridge: executes a list of <see cref="BaseAction"/>s against the active
    /// <see cref="GraphFlowDriver"/>'s context when <see cref="Fire"/> is called. Wire <c>Fire()</c>
    /// to any Unity event (button onClick, OnTriggerEnter, interaction callback, puzzle completion)
    /// to update graph state without writing custom scripts.
    /// <para>
    /// Optionally raises a named signal after the actions, and can be set to fire only once.
    /// The context is read from <see cref="GraphFlowDriver.Active"/> — the persistent cross-scene
    /// driver. If no driver is active, the trigger logs a warning and does nothing.
    /// </para>
    /// </summary>
    public class ContextTrigger : MonoBehaviour
    {
        [Tooltip("Actions executed on the active context when Fire() is called.")]
        [SerializeField] private List<BaseAction> _actions = new List<BaseAction>();

        [Tooltip("Optional signal raised on the context after actions execute.")]
        [SerializeField] private string _signal;

        [Tooltip("When true, Fire() does nothing after the first successful invocation.")]
        [SerializeField] private bool _fireOnce = true;

        [Tooltip("Optional GameObjects to activate when fired (e.g. a puzzle prefab, a VFX).")]
        [SerializeField] private List<GameObject> _activate = new List<GameObject>();

        [Tooltip("Optional GameObjects to deactivate when fired (e.g. hide the interactable).")]
        [SerializeField] private List<GameObject> _deactivate = new List<GameObject>();

        private bool _fired;

        /// <summary>True after the first successful <see cref="Fire"/> (when <see cref="_fireOnce"/> is on).</summary>
        public bool HasFired => _fired;

        /// <summary>
        /// Executes all configured actions against the active driver's context, toggles GameObjects,
        /// and optionally raises a signal. Call from a UnityEvent, an interaction system, or code.
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

            _fired = true;
            var ctx = driver.Context;

            foreach (var action in _actions)
                if (action != null) action.Execute(ctx);

            foreach (var go in _activate)
                if (go != null) go.SetActive(true);

            foreach (var go in _deactivate)
                if (go != null) go.SetActive(false);

            if (!string.IsNullOrEmpty(_signal))
                ctx.RaiseSignal(_signal);
        }

        /// <summary>Resets the fire-once guard so the trigger can fire again.</summary>
        public void ResetTrigger() => _fired = false;
    }
}
