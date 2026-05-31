using System;
using UnityEngine;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// A named visual expression for a <see cref="Speaker"/>: a logical key (e.g. "happy", "angry")
    /// and a presentation asset (prefab/sprite/etc.) the game UI instantiates. The asset is referenced
    /// only — spawning/animation is the game's concern, not this library's.
    /// </summary>
    [Serializable]
    public class SpeakerExpression
    {
        [SerializeField] private string _key;
        [SerializeField] private UnityEngine.Object _asset;

        /// <summary>Logical expression key (not translated). E.g. "neutral", "happy".</summary>
        public string Key { get => _key; set => _key = value; }

        /// <summary>Presentation asset for this expression (prefab, sprite, etc.).</summary>
        public UnityEngine.Object Asset { get => _asset; set => _asset = value; }
    }
}
