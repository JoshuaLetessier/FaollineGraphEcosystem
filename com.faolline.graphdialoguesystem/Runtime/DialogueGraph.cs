using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Concrete graph asset for an authored dialogue. Owns the dialogue's nodes, edges, parameters,
    /// entry point, and a stable <see cref="BaseGraph.GraphId"/> (inherited). Opened by
    /// <see cref="Faolline.GraphDialogue.Editor.DialogueGraphEditorWindow"/> and played by
    /// <see cref="DialoguePlayer"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Dialogue Graph", fileName = "NewDialogueGraph")]
    public class DialogueGraph : BaseGraph { }
}
