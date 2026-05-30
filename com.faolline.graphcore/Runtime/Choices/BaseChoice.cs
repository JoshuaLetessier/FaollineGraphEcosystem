using System;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A selectable option within a <see cref="ChoiceNodeData"/>.
    /// Optionally gated by a <see cref="BaseCondition"/>. Subclass in downstream libs
    /// to add domain-specific fields (e.g., localized display text).
    /// </summary>
    [Serializable]
    public class BaseChoice
    {
        [SerializeField, HideInInspector] private string _id;
        [SerializeField] private BaseCondition _condition;

        /// <summary>Unique identifier (GUID) for this choice.</summary>
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>Optional condition that gates this choice. Null means always available.</summary>
        public BaseCondition Condition
        {
            get => _condition;
            set => _condition = value;
        }
    }
}
