using System.IO;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Exposes <see cref="FaollineGraphSettings"/> under Edit ▸ Project Settings ▸ Faolline Graph, so a
    /// consumer without their own settings dashboard still has somewhere to point the constants generators.
    /// A consumer's own tooling can read/write <see cref="FaollineGraphSettings.instance"/> directly instead —
    /// both routes land on the same asset.
    /// </summary>
    internal static class FaollineGraphSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/Faolline Graph", SettingsScope.Project)
            {
                label = "Faolline Graph",
                keywords = new[] { "graph", "signal", "variable", "constants", "generate", "codegen" },
                guiHandler = _ =>
                {
                    var settings = FaollineGraphSettings.instance;

                    EditorGUILayout.HelpBox(
                        "Folder SignalConstantsGenerator/VariableConstantsGenerator write GraphSignals.cs/" +
                        "GraphVariables.cs into. Leave empty to keep each generator's default " +
                        "(Assets/Generated/ — no asmdef, compiles into Assembly-CSharp).",
                        MessageType.None);

                    EditorGUI.BeginChangeCheck();
                    var folder = EditorGUILayout.TextField("Generated Constants Folder", settings.GeneratedConstantsFolder);
                    if (EditorGUI.EndChangeCheck())
                        settings.GeneratedConstantsFolder = folder;

                    if (!string.IsNullOrEmpty(folder) && !HasAncestorAsmdef(folder))
                    {
                        EditorGUILayout.HelpBox(
                            $"No .asmdef found in or above '{folder}' — constants generated here will " +
                            "compile into Assembly-CSharp and be unreachable from any asmdef-defined assembly.",
                            MessageType.Warning);
                    }
                }
            };
        }

        /// <summary>
        /// Walks up from <paramref name="folder"/> toward <c>Assets</c>, returning <c>true</c> as soon as a
        /// directory containing an <c>.asmdef</c> is found — mirrors Unity's own "nearest enclosing asmdef"
        /// compilation rule, so a folder with no ancestor asmdef is exactly the folder that would compile
        /// into <c>Assembly-CSharp</c>.
        /// </summary>
        private static bool HasAncestorAsmdef(string folder)
        {
            var dir = folder.TrimEnd('/', '\\');
            while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets"))
            {
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.asmdef").Length > 0)
                    return true;

                var parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parent) || parent == dir) break;
                dir = parent;
            }
            return false;
        }
    }
}
