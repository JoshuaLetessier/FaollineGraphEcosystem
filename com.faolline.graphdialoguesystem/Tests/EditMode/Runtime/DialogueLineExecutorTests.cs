using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the dialogue line executor and registry factory.</summary>
    public class DialogueLineExecutorTests
    {
        [Test]
        public void NodeType_MatchesLineNode()
        {
            var exec = new DialogueLineExecutor();
            Assert.AreEqual(DialogueLineNodeData.NodeTypeId, exec.NodeType);
        }

        [Test]
        public void Execute_RecordsLastLine()
        {
            var exec = new DialogueLineExecutor();
            var line = new DialogueLineNodeData { Id = "n", NodeType = DialogueLineNodeData.NodeTypeId, TextKey = "k" };
            exec.Execute(line, new DialogueContext());
            Assert.AreSame(line, exec.LastLine);
        }

        [Test]
        public void Factory_RegistersLineExecutor()
        {
            var registry = DialogueExecutorRegistryFactory.Create(out var exec);
            Assert.IsNotNull(registry.GetExecutor(DialogueLineNodeData.NodeTypeId));
            Assert.AreSame(exec, registry.GetExecutor(DialogueLineNodeData.NodeTypeId));
        }
    }
}
