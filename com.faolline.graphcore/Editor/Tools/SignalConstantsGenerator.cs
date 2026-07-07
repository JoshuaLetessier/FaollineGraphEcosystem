using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Generates a <c>GraphSignals</c> static class of <c>const string</c>s from the project's
    /// <see cref="SignalDef"/> assets — the compile-checked bridge for raising asset signals from pure host
    /// code. Each constant's SYMBOL is derived from the signal's <see cref="SignalDef.DisplayName"/> (legible
    /// in code, e.g. <c>GraphSignals.PlayerInteracted</c>) and its VALUE is the signal's stable GUID
    /// (<see cref="SignalDef.Key"/>, the runtime key). Renaming a signal's display name changes only the
    /// symbol (breaking stale code at compile — the intended, safe rename), never the value/GUID (awaits,
    /// raises, and saves keep matching). Menu: <c>Faolline ▸ Signals ▸ Generate Constants</c>.
    /// <para>
    /// The generator ships in graphcore; the generated class lives in the consumer project (each game has its
    /// own signals). It is a plain <c>const string</c> class — zero dependencies.
    /// </para>
    /// </summary>
    public static class SignalConstantsGenerator
    {
        /// <summary>Default output path for the generated class (consumer project).</summary>
        public const string DefaultOutputPath = "Assets/Generated/GraphSignals.cs";

        /// <summary>The generated class name.</summary>
        public const string ClassName = "GraphSignals";

        [MenuItem("Faolline/Signals/Generate Constants")]
        private static void GenerateMenu()
        {
            var signals = new List<(string displayName, string guid)>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(SignalDef)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sig = AssetDatabase.LoadAssetAtPath<SignalDef>(path);
                if (sig != null && !string.IsNullOrEmpty(sig.Key))
                    signals.Add((sig.DisplayName, sig.Key));
            }

            if (signals.Count == 0)
            {
                Debug.Log("[GraphCore] No SignalDef assets found — nothing to generate.");
                return;
            }

            if (!TryBuildSource(signals, out var source, out var errors))
            {
                Debug.LogError("[GraphCore] Signal constant generation aborted (fix the display names and " +
                    "regenerate):\n - " + string.Join("\n - ", errors));
                return;
            }

            var dir = Path.GetDirectoryName(DefaultOutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(DefaultOutputPath, source);
            AssetDatabase.ImportAsset(DefaultOutputPath);
            Debug.Log($"[GraphCore] Generated {signals.Count} signal constant(s) → {DefaultOutputPath}");
        }

        /// <summary>
        /// Pure, testable core: builds the <c>GraphSignals</c> C# source from (display name, GUID) pairs.
        /// Returns <c>false</c> without producing source when two display names sanitize to the same symbol
        /// (a blocking collision — never a silent merge or auto-suffix); <paramref name="errors"/> then lists
        /// each collision. Otherwise <paramref name="source"/> is the file text. Delegates to the shared
        /// <see cref="ConstantsGeneratorCore"/> (see also <see cref="VariableConstantsGenerator"/>).
        /// </summary>
        public static bool TryBuildSource(
            IReadOnlyList<(string displayName, string guid)> signals, out string source, out List<string> errors)
            => ConstantsGeneratorCore.TryBuildSource(
                ClassName, "signal", "Signals > Generate Constants", signals, out source, out errors);

        /// <summary>
        /// Turns a display name into a valid PascalCase C# identifier: word breaks on any non-alphanumeric run,
        /// each word capitalized; a leading digit is prefixed with <c>_</c>; empty/all-invalid yields <c>_</c>.
        /// </summary>
        public static string Sanitize(string displayName) => ConstantsGeneratorCore.Sanitize(displayName);
    }
}
