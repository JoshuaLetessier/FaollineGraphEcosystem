using System;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// A statement node with an editable <see cref="Label"/> field.
    /// Used in the GraphTest verification package to exercise the inspector and runtime.
    /// </summary>
    [Serializable]
    public class TestStatementNodeData : StatementNodeData
    {
        /// <summary>Canonical type identifier for GraphTest statement nodes.</summary>
        public const string NodeTypeId = "graphtest/statement";

        [SerializeField] private string _label = string.Empty;

        /// <summary>Editable display text for this statement node.</summary>
        public string Label
        {
            get => _label;
            set => _label = value ?? string.Empty;
        }
    }
}
