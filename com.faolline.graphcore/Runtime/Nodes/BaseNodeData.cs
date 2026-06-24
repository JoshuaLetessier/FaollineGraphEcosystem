using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Abstract base for all graph node data. Carries universal identity, editor visual metadata,
    /// and lifecycle hooks (<see cref="EntryConditions"/>, <see cref="OnEnterActions"/>,
    /// <see cref="OnExitActions"/>) present on every node regardless of type or downstream lib.
    /// </summary>
    [Serializable]
    public abstract class BaseNodeData
    {
        [SerializeField, HideInInspector] private string _id;
        [SerializeField, HideInInspector] private string _nodeType;

        [Header("Appearance")]
        [SerializeField, Tooltip("Display name shown on the node title bar. When empty, the node type label is used. Also serves as source text for localization.")]
        private string _title = string.Empty;
        [SerializeField] private Vector2 _position;
        [SerializeField, Tooltip("When enabled, this node uses the custom Node Color instead of the default type color in the editor.")]
        private bool  _hasColorOverride;
        [SerializeField, Tooltip("Custom editor display color for this node. Only applied when Has Color Override is enabled.")]
        private Color _nodeColor;

        [Header("Entry / Exit Logic")]
        [SerializeField, Tooltip("Conditions checked before the runner enters this node. All must pass (AND). Use to gate access to a node based on context state.")]
        private List<BaseCondition> _entryConditions  = new List<BaseCondition>();
        [SerializeField, Tooltip("Actions executed when the runner enters this node, after entry conditions pass.")]
        private List<BaseAction>    _onEnterActions   = new List<BaseAction>();
        [SerializeField, Tooltip("Actions executed when the runner exits this node, before traversing the next edge.")]
        private List<BaseAction>    _onExitActions    = new List<BaseAction>();

        [Header("Signals & Timing")]
        [SerializeField, Tooltip("SignalName asset the runner waits for on entry. The node parks (WaitingForSignal) until a matching signal is raised. Takes precedence over the raw string field below.")]
        private SignalName _awaitSignalAsset;
        [SerializeField, Tooltip("Raw signal name string (used when no SignalName asset is assigned above). The node parks until this signal is raised on the runner.")]
        private string _awaitSignal = string.Empty;
        [SerializeField, Tooltip("Extra conditions an await-signal must satisfy to resume this node. All must pass (AND). Unlike Entry Conditions (checked once on arrival), these are re-checked every time the signal fires — the node stays parked until the context satisfies them.")]
        private List<BaseCondition> _resumeConditions = new List<BaseCondition>();
        [SerializeField, Tooltip("Seconds the node waits before advancing (0 = no wait). If an await signal is also set, the signal takes precedence and this duration is ignored.")]
        private float  _waitDuration;

        [Header("Checkpoint")]
        [SerializeField, Tooltip("When enabled, GoBackToCheckpoint can restore the runner to this node. Also marks a valid capture point for GraphSave snapshots.")]
        private bool  _isCheckpoint;

        /// <summary>Unique identifier (GUID) for this node.</summary>
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>
        /// String identifier for the node type. MUST equal the subclass's <c>NodeTypeId</c> const.
        /// </summary>
        public string NodeType
        {
            get => _nodeType;
            set => _nodeType = value;
        }

        /// <summary>
        /// Optional editor-facing display name shown on the node title bar (editable inline). When empty,
        /// the node view falls back to its type label. Purely visual metadata to make graphs readable;
        /// downstream libs may also read it (e.g. as default/source text). Never null.
        /// </summary>
        public string Title
        {
            get => _title;
            set => _title = value ?? string.Empty;
        }

        /// <summary>Editor canvas position of this node.</summary>
        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        /// <summary>
        /// Conditions evaluated before the runtime enters this node. Never null.
        /// </summary>
        public List<BaseCondition> EntryConditions => _entryConditions;

        /// <summary>
        /// Optional gate a matching await-signal (<see cref="AwaitSignalName"/>) must pass to resume this
        /// parked node. All must pass (AND); a null entry is skipped. Empty (the default) means no gate — the
        /// node resumes on a name match alone, as before. A raise that fails the gate is ignored and the node
        /// stays parked (re-armable): the actor can raise the signal again once the context satisfies the
        /// conditions. Never null.
        /// </summary>
        public List<BaseCondition> ResumeConditions => _resumeConditions;

        /// <summary>Actions executed when the runtime enters this node. Never null.</summary>
        public List<BaseAction> OnEnterActions => _onEnterActions;

        /// <summary>Actions executed when the runtime exits this node. Never null.</summary>
        public List<BaseAction> OnExitActions => _onExitActions;

        /// <summary>When <c>true</c>, this node is treated as a save/checkpoint point.</summary>
        public bool IsCheckpoint
        {
            get => _isCheckpoint;
            set => _isCheckpoint = value;
        }

        /// <summary>
        /// When <c>true</c>, <see cref="NodeColor"/> overrides the default editor display color.
        /// </summary>
        public bool HasColorOverride
        {
            get => _hasColorOverride;
            set => _hasColorOverride = value;
        }

        /// <summary>Editor display color. Only meaningful when <see cref="HasColorOverride"/> is true.</summary>
        public Color NodeColor
        {
            get => _nodeColor;
            set => _nodeColor = value;
        }

        /// <summary>
        /// When non-empty, entering this node holds execution (<see cref="RunnerState.WaitingForSignal"/>)
        /// until a signal of this name is raised on the runner; the runner then advances normally. Empty
        /// (the default) means the node does not wait. Append-only metadata, universal to every node type.
        /// </summary>
        public string AwaitSignalName
        {
            get => _awaitSignalAsset != null ? (string)_awaitSignalAsset : _awaitSignal;
            set => _awaitSignal = value ?? string.Empty;
        }

        /// <summary>
        /// When greater than zero, entering this node holds execution (<see cref="RunnerState.WaitingForTime"/>)
        /// for this many seconds of host-fed time (via <see cref="BaseRunner.Tick"/>) before advancing. Zero
        /// (the default) means no timed hold. If <see cref="AwaitSignalName"/> is also set, the signal wait
        /// takes precedence and this duration is ignored. Append-only metadata.
        /// </summary>
        public float WaitDuration
        {
            get => _waitDuration;
            set => _waitDuration = value;
        }

    }
}
