using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Runner-agnostic resolution of dialogue nodes into displayable steps. Given a dialogue node and a
    /// <see cref="BaseContext"/>, it produces the same <see cref="LineStep"/> / <see cref="ChoiceStep"/> a
    /// <see cref="DialoguePlayer"/> emits — but for a node owned by <em>any</em> runner. This lets a host
    /// (e.g. a gameflow driver that embeds a dialogue subgraph) <b>render</b> dialogue without owning a
    /// <see cref="DialoguePlayer"/>. <see cref="DialoguePlayer"/> itself now resolves through this presenter.
    /// </summary>
    public sealed class DialoguePresenter
    {
        private readonly ILocalizationProvider _localization;
        private readonly ILocalizedAssetProvider _assets;
        private readonly Func<string, Speaker> _speakerLookup;
        private readonly LocalizationStrictMode _strictMode;
        private readonly List<string> _missingKeys = new List<string>();

        /// <summary>Keys the provider could not resolve (recorded in Audit/Strict modes).</summary>
        public IReadOnlyList<string> MissingKeys => _missingKeys;

        /// <summary>Raised once per missing localization key (Audit mode), before any Strict throw.</summary>
        public event Action<string> OnMissingKey;

        /// <summary>Clears the recorded missing keys (e.g. on a session restore), re-arming Audit reporting.</summary>
        public void ClearMissingKeys() => _missingKeys.Clear();

        public DialoguePresenter(
            ILocalizationProvider localization,
            ILocalizedAssetProvider assets = null,
            Func<string, Speaker> speakerLookup = null,
            LocalizationStrictMode strictMode = LocalizationStrictMode.Permissive)
        {
            _localization = localization ?? new CsvLocalizationProvider(string.Empty, "en");
            _assets = assets;
            _speakerLookup = speakerLookup;
            _strictMode = strictMode;
        }

        /// <summary>
        /// Resolves <paramref name="node"/> to a <see cref="LineStep"/> or <see cref="ChoiceStep"/>, or
        /// <c>null</c> when it is not a dialogue node (so a host can call this for every entered node).
        /// </summary>
        public DialogueStep Resolve(BaseNodeData node, BaseContext context)
        {
            if (node is DialogueLineNodeData line) return ResolveLine(line, context);
            if (node is ChoiceNodeData choice) return ResolveChoice(choice, context);
            return null;
        }

        /// <summary>Resolved speaker name + localized/interpolated text + expression key + voice for a line.</summary>
        public LineStep ResolveLine(DialogueLineNodeData line, BaseContext context)
        {
            if (line == null) return null;
            string text = ResolveChecked(DialogueLocalizationKeys.ForLine(line));
            text = DialogueTextInterpolator.Interpolate(text, context);
            string speakerName = ResolveSpeakerName(line.SpeakerKey);

            // Voice is resolved by the line's key from the localized asset tables (no per-node clip).
            var voice = _assets != null
                ? _assets.ResolveAsset<AudioClip>(DialogueLocalizationKeys.ForLine(line))
                : null;

            return new LineStep(line.Id, line.SpeakerKey, speakerName, text, line.ExpressionKey, voice);
        }

        /// <summary>Options with resolved label + availability (each option's condition against the context).</summary>
        public ChoiceStep ResolveChoice(ChoiceNodeData choiceNode, BaseContext context)
        {
            if (choiceNode == null) return null;
            var options = new List<ChoiceOption>();
            foreach (var baseChoice in choiceNode.Choices)
            {
                if (baseChoice == null) continue;
                string labelKey = DialogueLocalizationKeys.ForChoice(baseChoice);
                string label = string.IsNullOrEmpty(labelKey)
                    ? baseChoice.Id
                    : ResolveChecked(labelKey);
                label = DialogueTextInterpolator.Interpolate(label, context);
                bool available = baseChoice.Condition == null || baseChoice.Condition.Evaluate(context);
                options.Add(new ChoiceOption(baseChoice.Id, label, available));
            }
            return new ChoiceStep(choiceNode.Id, options);
        }

        private string ResolveSpeakerName(string speakerKey)
        {
            if (string.IsNullOrEmpty(speakerKey)) return string.Empty;
            var speaker = _speakerLookup?.Invoke(speakerKey);
            if (speaker == null) return speakerKey;

            var nameKey = DialogueLocalizationKeys.ForSpeaker(speaker);
            if (!string.IsNullOrEmpty(nameKey))
            {
                var resolved = _localization.Resolve(nameKey, _localization.CurrentLocale);
                if (!string.IsNullOrEmpty(resolved) && resolved != $"#{nameKey}")
                    return resolved;
            }
            return string.IsNullOrEmpty(speaker.DisplayNameFallback) ? speakerKey : speaker.DisplayNameFallback;
        }

        /// <summary>
        /// Resolves a key through the provider, applying the configured <see cref="LocalizationStrictMode"/>
        /// when the key is missing: Permissive returns the <c>#key</c> fallback silently; Audit warns + records
        /// it (and raises <see cref="OnMissingKey"/>); Strict throws.
        /// </summary>
        private string ResolveChecked(string key)
        {
            var locale = _localization.CurrentLocale;
            var value = _localization.Resolve(key, locale);

            bool missing = string.IsNullOrEmpty(value) || value == $"#{key}";
            if (!missing) return value;

            switch (_strictMode)
            {
                case LocalizationStrictMode.Strict:
                    throw new LocalizationException(key, locale);
                case LocalizationStrictMode.Audit:
                    if (!_missingKeys.Contains(key))
                    {
                        _missingKeys.Add(key);
                        Debug.LogWarning($"[GraphDialogue] Missing localization key '{key}' for locale '{locale}'.");
                        OnMissingKey?.Invoke(key);
                    }
                    break;
                // Permissive: return the fallback silently.
            }
            return value;
        }
    }
}
