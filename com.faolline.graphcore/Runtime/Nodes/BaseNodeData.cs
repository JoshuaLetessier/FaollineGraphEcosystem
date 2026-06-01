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
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private Vector2 _position;
        [SerializeField] private List<BaseCondition> _entryConditions  = new List<BaseCondition>();
        [SerializeField] private List<BaseAction>    _onEnterActions   = new List<BaseAction>();
        [SerializeField] private List<BaseAction>    _onExitActions    = new List<BaseAction>();

        [SerializeField] private bool  _isCheckpoint;
        [SerializeField] private bool  _hasColorOverride;
        [SerializeField] private Color _nodeColor;

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
    }
}
