using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Represents a directed connection between two nodes in a <see cref="BaseGraph"/>.
    /// Optionally carries a <see cref="BaseCondition"/> that gates traversal.
    /// </summary>
    [Serializable]
    public class BaseEdgeData
    {
        [SerializeField, HideInInspector] private string        _id;
        [SerializeField, HideInInspector] private string        _fromNodeId;
        [SerializeField, HideInInspector] private string        _toNodeId;
        [SerializeField] private string        _portName;
        [SerializeField] private BaseCondition _condition;
        [SerializeField] private bool          _hasColorOverride;
        [SerializeField] private Color         _edgeColor;
        [SerializeField, HideInInspector] private List<Vector2> _waypoints = new List<Vector2>();

        /// <summary>Unique identifier (GUID) for this edge.</summary>
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>Id of the source node.</summary>
        public string FromNodeId
        {
            get => _fromNodeId;
            set => _fromNodeId = value;
        }

        /// <summary>Id of the target node.</summary>
        public string ToNodeId
        {
            get => _toNodeId;
            set => _toNodeId = value;
        }

        /// <summary>Output port identifier on the source node.</summary>
        public string PortName
        {
            get => _portName;
            set => _portName = value;
        }

        /// <summary>Optional condition that gates this edge. Null means always traversable.</summary>
        public BaseCondition Condition
        {
            get => _condition;
            set => _condition = value;
        }

        /// <summary>
        /// When <c>true</c>, <see cref="EdgeColor"/> overrides the default editor display color.
        /// </summary>
        public bool HasColorOverride
        {
            get => _hasColorOverride;
            set => _hasColorOverride = value;
        }

        /// <summary>Editor display color. Only meaningful when <see cref="HasColorOverride"/> is true.</summary>
        public Color EdgeColor
        {
            get => _edgeColor;
            set => _edgeColor = value;
        }

        /// <summary>
        /// Optional editor-only bend points (graph-space) the edge routes through, in order from source to
        /// target. Empty = a direct (auto-routed) edge. Purely visual metadata — like <see cref="BaseNodeData.Position"/>,
        /// it has no runtime effect and is persisted with the graph. Never null.
        /// </summary>
        public List<Vector2> Waypoints => _waypoints;
    }
}
