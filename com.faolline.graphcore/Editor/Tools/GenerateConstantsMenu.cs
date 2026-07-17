using UnityEditor;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Single entry point regenerating both codegen'd constants classes — <c>GraphSignals</c>
    /// (<see cref="SignalConstantsGenerator"/>) and <c>GraphVariables</c> (<see cref="VariableConstantsGenerator"/>)
    /// — since a consumer touching either signals or variables typically wants both refreshed together.
    /// </summary>
    public static class GenerateConstantsMenu
    {
        [MenuItem("Faolline/Graph/Generate Constants")]
        private static void GenerateAll()
        {
            SignalConstantsGenerator.Generate();
            VariableConstantsGenerator.Generate();
        }
    }
}
