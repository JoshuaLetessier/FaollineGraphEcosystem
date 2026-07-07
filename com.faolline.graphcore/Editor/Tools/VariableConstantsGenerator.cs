using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Generates a <c>GraphVariables</c> static class of <c>const string</c>s from the project's
    /// <see cref="VariableDef"/> assets — the compile-checked bridge for reading/writing asset parameters from
    /// pure host code. Each constant's SYMBOL is derived from the parameter's <see cref="VariableDef.DisplayName"/>
    /// (legible in code, e.g. <c>GraphVariables.Hp</c>) and its VALUE is the parameter's stable GUID
    /// (<see cref="VariableDef.Key"/>, the runtime key). Renaming a parameter's display name changes only the
    /// symbol (breaking stale code at compile — the intended, safe rename), never the value/GUID (sets, gets, and
    /// saves keep matching). Menu: <c>Faolline ▸ Variables ▸ Generate Constants</c>.
    /// <para>
    /// The generator ships in graphcore; the generated class lives in the consumer project (each game has its own
    /// parameters). It is a plain <c>const string</c> class — zero dependencies. Mirror of
    /// <see cref="SignalConstantsGenerator"/>, sharing <see cref="ConstantsGeneratorCore"/>.
    /// </para>
    /// </summary>
    public static class VariableConstantsGenerator
    {
        /// <summary>Default output path for the generated class (consumer project).</summary>
        public const string DefaultOutputPath = "Assets/Generated/GraphVariables.cs";

        /// <summary>The generated class name.</summary>
        public const string ClassName = "GraphVariables";

        [MenuItem("Faolline/Variables/Generate Constants")]
        private static void GenerateMenu()
        {
            var parameters = new List<(string displayName, string guid)>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(VariableDef)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var p = AssetDatabase.LoadAssetAtPath<VariableDef>(path);
                if (p != null && !string.IsNullOrEmpty(p.Key))
                    parameters.Add((p.DisplayName, p.Key));
            }

            if (parameters.Count == 0)
            {
                Debug.Log("[GraphCore] No VariableDef assets found — nothing to generate.");
                return;
            }

            if (!TryBuildSource(parameters, out var source, out var errors))
            {
                Debug.LogError("[GraphCore] Variable constant generation aborted (fix the display names and " +
                    "regenerate):\n - " + string.Join("\n - ", errors));
                return;
            }

            var dir = Path.GetDirectoryName(DefaultOutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(DefaultOutputPath, source);
            AssetDatabase.ImportAsset(DefaultOutputPath);
            Debug.Log($"[GraphCore] Generated {parameters.Count} parameter constant(s) → {DefaultOutputPath}");
        }

        /// <summary>
        /// Pure, testable core: builds the <c>GraphVariables</c> C# source from (display name, GUID) pairs.
        /// Blocking error (no source) on a symbol collision. Delegates to the shared
        /// <see cref="ConstantsGeneratorCore"/>.
        /// </summary>
        public static bool TryBuildSource(
            IReadOnlyList<(string displayName, string guid)> parameters, out string source, out List<string> errors)
            => ConstantsGeneratorCore.TryBuildSource(
                ClassName, "parameter", "Variables > Generate Constants", parameters, out source, out errors);

        /// <summary>PascalCase-sanitizes a display name into a C# identifier (see <see cref="ConstantsGeneratorCore.Sanitize"/>).</summary>
        public static string Sanitize(string displayName) => ConstantsGeneratorCore.Sanitize(displayName);
    }
}
