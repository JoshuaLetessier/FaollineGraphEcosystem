using System;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>A typed, named variable scoped to a single <see cref="BaseGraph"/>.</summary>
    [Serializable]
    public class ParameterData
    {
        [SerializeField] private string _key;
        [SerializeField] private ParameterType _type;
        [SerializeField] private string _defaultValue;

        /// <summary>Variable name. Uniqueness is enforced by the runtime, not the data layer.</summary>
        public string Key
        {
            get => _key;
            set => _key = value;
        }

        /// <summary>The data type of this parameter.</summary>
        public ParameterType Type
        {
            get => _type;
            set => _type = value;
        }

        /// <summary>
        /// String representation of the default value.
        /// Parsing and conversion are handled by the runtime layer.
        /// </summary>
        public string DefaultValue
        {
            get => _defaultValue;
            set => _defaultValue = value;
        }
    }
}
