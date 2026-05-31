using System;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// A selectable dialogue option. Extends <see cref="BaseChoice"/> (which already carries the
    /// stable <c>Id</c> used as the output port routing key, and an optional gating <c>Condition</c>)
    /// with a localized label key shown to the player.
    /// </summary>
    [Serializable]
    public class DialogueChoice : BaseChoice
    {
        [SerializeField] private string _displayTextKey = string.Empty;

        /// <summary>Localization key for this choice's displayed label. Never null.</summary>
        public string DisplayTextKey
        {
            get => _displayTextKey;
            set => _displayTextKey = value ?? string.Empty;
        }
    }
}
