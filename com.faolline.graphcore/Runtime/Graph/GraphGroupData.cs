using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Serializable data for a visual node group on the canvas. Groups are purely an authoring
    /// aid — they have no runtime effect. Each group tracks its contained node IDs, title, color,
    /// canvas position/size, and collapsed state.
    /// </summary>
    [Serializable]
    public class GraphGroupData
    {
        [SerializeField] private string _id;
        [SerializeField] private string _title = "Group";
        [SerializeField] private Color  _color = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        [SerializeField] private Vector2 _position;
        [SerializeField] private Vector2 _size = new Vector2(320, 200);
        [SerializeField] private List<string> _nodeIds = new List<string>();
        [SerializeField] private bool _isCollapsed;

        /// <summary>Stable GUID for this group.</summary>
        public string Id { get => _id; set => _id = value; }

        /// <summary>Display title shown in the group header.</summary>
        public string Title { get => _title; set => _title = value; }

        /// <summary>Background tint color of the group.</summary>
        public Color Color { get => _color; set => _color = value; }

        /// <summary>Canvas position of the group's top-left corner.</summary>
        public Vector2 Position { get => _position; set => _position = value; }

        /// <summary>Canvas size of the group.</summary>
        public Vector2 Size { get => _size; set => _size = value; }

        /// <summary>Node IDs contained in this group.</summary>
        public List<string> NodeIds => _nodeIds;

        /// <summary>Whether the group is collapsed (content area hidden) on the canvas.</summary>
        public bool IsCollapsed { get => _isCollapsed; set => _isCollapsed = value; }
    }
}
