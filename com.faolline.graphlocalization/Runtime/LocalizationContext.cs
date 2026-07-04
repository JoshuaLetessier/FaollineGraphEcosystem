namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Ambient accessor for the current <see cref="LocalizationSettings"/>.
    /// Prefer explicit injection where possible; use this for code paths that cannot receive a provider.
    /// </summary>
    public static class LocalizationContext
    {
        private static LocalizationSettings _current;

#if UNITY_EDITOR
        // With Enter Play Mode Options (domain reload disabled), statics survive the Edit/Play boundary:
        // without this reset a session would reuse whatever settings/provider edit-mode tooling (e.g. a
        // dialogue preview window) last left here, instead of loading fresh from the settings asset.
        // Editor-only — a player build always starts fresh.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _current = null;
#endif

        public static LocalizationSettings Current
        {
            get
            {
                if (_current == null)
                {
                    var asset = LocalizationSettingsLoader.Load();
                    _current = asset != null ? asset.CreateSettings() : new LocalizationSettings();
                }
                return _current;
            }
            set => _current = value;
        }

        public static string Resolve(string key) => Current.Resolve(key);
    }
}
