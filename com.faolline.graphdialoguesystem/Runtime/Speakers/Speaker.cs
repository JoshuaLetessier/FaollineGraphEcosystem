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
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/Assets/FaollineGraphEcosystem/com.faolline.graphdialoguesystem/README.md")]
    public class Speaker : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Logical id referenced by DialogueLineNodeData.SpeakerKey. Not translated.")]
        private string _speakerId = string.Empty;
        [SerializeField, Tooltip("Literal display name used when the localization key cannot be resolved. Also serves as the source text pre-filled into the localization table.")]
        private string _displayNameFallback = "Speaker";
        [SerializeField, Tooltip("Tint applied to this speaker's name in the built-in dialogue views.")]
        private Color _nameColor = Color.white;

        [Header("Expressions")]
        [SerializeField, Tooltip("Named expressions (key to presentation asset). The expression key on a dialogue line selects from this list.")]
        private List<SpeakerExpression> _expressions = new List<SpeakerExpression>();
        [SerializeField, Tooltip("Presentation asset used when a requested expression key has no match in the expressions list.")]
        private UnityEngine.Object _fallbackExpression;

        /// <summary>Logical id referenced by <see cref="DialogueLineNodeData.SpeakerKey"/>. Not translated.</summary>
        public string SpeakerId { get => _speakerId; set => _speakerId = value; }

        // The display-name localization key is NOT stored — it is derived from SpeakerId via
        // DialogueLocalizationKeys.ForSpeaker(this). DisplayNameFallback is the source text + runtime fallback.

        /// <summary>Literal display name used when the key cannot be resolved. Also the source text pre-filled into the table.</summary>
        public string DisplayNameFallback { get => _displayNameFallback; set => _displayNameFallback = value; }

        /// <summary>Tint applied to this speaker's name in the built-in views (defaults to white).</summary>
        public Color NameColor { get => _nameColor; set => _nameColor = value; }

        /// <summary>Read-only list of named expressions.</summary>
        public IReadOnlyList<SpeakerExpression> Expressions => _expressions;

        /// <summary>Adds an expression (key → presentation asset). No-op for an empty key.</summary>
        public void AddExpression(string key, UnityEngine.Object asset = null)
        {
            if (string.IsNullOrEmpty(key)) return;
            _expressions.Add(new SpeakerExpression { Key = key, Asset = asset });
        }

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
