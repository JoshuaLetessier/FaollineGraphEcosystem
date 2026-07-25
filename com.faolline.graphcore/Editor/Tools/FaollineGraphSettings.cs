using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Project-level settings for the ecosystem's editor tooling — currently just where
    /// <see cref="SignalConstantsGenerator"/>/<see cref="VariableConstantsGenerator"/> write their generated
    /// classes. Stored under <c>ProjectSettings/</c> (versioned, shared by the whole team) rather than
    /// <c>EditorPrefs</c> (per-machine) — this is a project architecture decision, not a personal editor
    /// preference. Also keeps the package itself write-free: <c>ProjectSettings/</c> belongs to the
    /// consumer project, not to a (possibly read-only, package-cached) package folder.
    /// </summary>
    [FilePath("ProjectSettings/FaollineGraphSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class FaollineGraphSettings : ScriptableSingleton<FaollineGraphSettings>
    {
        [SerializeField]
        [Tooltip("Folder the constants generators write GraphSignals.cs/GraphVariables.cs into. Empty " +
            "(the default) keeps each generator's own DefaultOutputPath (Assets/Generated/ — no asmdef, " +
            "compiles into Assembly-CSharp). Point this at a folder covered by your own asmdef instead.")]
        private string _generatedConstantsFolder = "";

        /// <summary>
        /// Folder the constants generators write into; empty means "use each generator's own
        /// <c>DefaultOutputPath</c>" (unchanged, pre-existing behavior). Setting this persists immediately.
        /// </summary>
        public string GeneratedConstantsFolder
        {
            get => _generatedConstantsFolder;
            set
            {
                _generatedConstantsFolder = value ?? "";
                Save(true);
            }
        }
    }
}
