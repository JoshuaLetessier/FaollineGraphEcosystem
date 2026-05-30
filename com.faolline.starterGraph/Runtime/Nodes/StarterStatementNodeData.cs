using System;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// A statement node with an editable <see cref="Label"/> field.
    /// Used in the StarterGraph verification package to exercise the inspector and runtime.
    /// </summary>
    [Serializable]
    public class StarterStatementNodeData : StatementNodeData
    {
        /// <summary>Canonical type identifier for StarterGraph statement nodes.</summary>
        public const string NodeTypeId = "startergraph/statement";

        [SerializeField] private string _label = string.Empty;

        /// <summary>Editable display text for this statement node.</summary>
        public string Label
        {
            get => _label;
            set => _label = value ?? string.Empty;
        }
    }
}
