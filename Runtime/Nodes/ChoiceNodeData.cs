using System;
using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A branching point that presents one or more choices to the runtime.
    /// Each choice may carry an optional <see cref="BaseCondition"/>. The
    /// <see cref="Choices"/> list is extensible — downstream libs subclass
    /// <see cref="BaseChoice"/> to add domain-specific fields.
    /// </summary>
    [Serializable]
    public class ChoiceNodeData : BaseNodeData
    {
        /// <summary>Canonical type identifier for choice nodes.</summary>
        public const string NodeTypeId = "graphcore/choice";

        [SerializeReference]
        private List<BaseChoice> _choices = new List<BaseChoice>();

        /// <summary>The available choices at this node. Never null. Extensible via subclasses.</summary>
        public List<BaseChoice> Choices => _choices;
    }
}
