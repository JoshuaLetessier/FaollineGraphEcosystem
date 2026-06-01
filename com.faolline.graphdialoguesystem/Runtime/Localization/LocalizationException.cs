using System;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Thrown by the player when a localization key cannot be resolved and the active
    /// <see cref="LocalizationStrictMode"/> is <see cref="LocalizationStrictMode.Strict"/>.
    /// </summary>
    public sealed class LocalizationException : Exception
    {
        /// <summary>The key that failed to resolve.</summary>
        public string Key { get; }

        /// <summary>The locale in which resolution was attempted.</summary>
        public string Locale { get; }

        public LocalizationException(string key, string locale)
            : base($"[GraphDialogue] Missing localization key '{key}' for locale '{locale}'.")
        {
            Key = key;
            Locale = locale;
        }
    }
}
