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

        [SerializeReference, Tooltip("Branching options presented at this node. Each choice has a title, an optional condition gate, and a unique id used by ChooseById.")]
        private List<BaseChoice> _choices = new List<BaseChoice>();

        /// <summary>The available choices at this node. Never null. Extensible via subclasses.</summary>
        public List<BaseChoice> Choices => _choices;
    }
}
