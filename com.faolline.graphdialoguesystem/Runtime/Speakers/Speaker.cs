using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// An interlocutor in a dialogue. Carries a logical id (referenced by
    /// <see cref="DialogueLineNodeData.SpeakerKey"/>), a localizable display name (resolved through an
    /// <see cref="ILocalizationProvider"/>) with a literal fallback, and a set of named expressions
    /// (key → presentation asset) with a fallback. Presentation is the game's concern; this type only
    /// carries data.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Speaker", fileName = "NewSpeaker")]
    public class Speaker : ScriptableObject
    {
        [SerializeField] private string _speakerId = string.Empty;
        [SerializeField] private string _displayNameKey = string.Empty;
        [SerializeField] private string _displayNameFallback = "Speaker";
        [SerializeField] private List<SpeakerExpression> _expressions = new List<SpeakerExpression>();
        [SerializeField] private UnityEngine.Object _fallbackExpression;

        /// <summary>Logical id referenced by <see cref="DialogueLineNodeData.SpeakerKey"/>. Not translated.</summary>
        public string SpeakerId { get => _speakerId; set => _speakerId = value; }

        /// <summary>Localization key for this speaker's display name.</summary>
        public string DisplayNameKey { get => _displayNameKey; set => _displayNameKey = value; }

        /// <summary>Literal display name used when the key cannot be resolved.</summary>
        public string DisplayNameFallback { get => _displayNameFallback; set => _displayNameFallback = value; }

        /// <summary>Read-only list of named expressions.</summary>
        public IReadOnlyList<SpeakerExpression> Expressions => _expressions;

        /// <summary>Asset used when a requested expression key is unknown.</summary>
        public UnityEngine.Object FallbackExpression { get => _fallbackExpression; set => _fallbackExpression = value; }

        /// <summary>
        /// Tries to resolve the presentation asset for <paramref name="key"/>. Returns true with the
        /// matching asset, otherwise true with the <see cref="FallbackExpression"/> if set, else false.
        /// Null-safe — never throws.
        /// </summary>
        public bool TryGetExpression(string key, out UnityEngine.Object asset)
        {
            if (!string.IsNullOrEmpty(key))
            {
                foreach (var e in _expressions)
                {
                    if (e != null && e.Key == key && e.Asset != null)
                    {
                        asset = e.Asset;
                        return true;
                    }
                }
            }

            if (_fallbackExpression != null)
            {
                asset = _fallbackExpression;
                return true;
            }

            asset = null;
            return false;
        }
    }
}
