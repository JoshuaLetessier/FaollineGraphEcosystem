using System;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Concrete <see cref="BaseChoice"/> for the StarterGraph verification package.
    /// Adds a single human-readable <see cref="Label"/> used as the displayed text on a
    /// choice node's output port and in the inspector. The inherited <c>Id</c> (GUID) is
    /// used as the output port's <c>portName</c> for runtime routing via <c>ChooseById</c>.
    /// </summary>
    [Serializable]
    public class StarterChoice : BaseChoice
    {
        [SerializeField] private string _label = string.Empty;

        /// <summary>Human-readable choice text. Never null.</summary>
        public string Label
        {
            get => _label;
            set => _label = value ?? string.Empty;
        }
    }
}
