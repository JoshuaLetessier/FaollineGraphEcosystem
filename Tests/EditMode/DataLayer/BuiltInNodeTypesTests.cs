using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    public class BuiltInNodeTypesTests
    {
        // T010: EndReason enum

        [Test]
        public void EndReason_HasExactlyThreeValues()
        {
            Assert.AreEqual(3, System.Enum.GetValues(typeof(EndReason)).Length,
                "EndReason must have exactly 3 values.");
        }

        [Test]
        public void EndReason_HasCorrectIntegerValues()
        {
            Assert.AreEqual(0, (int)EndReason.Completed);
            Assert.AreEqual(1, (int)EndReason.Cancelled);
            Assert.AreEqual(2, (int)EndReason.Error);
        }

        // T040: StartNodeData

        [Test]
        public void StartNodeData_NodeTypeId_IsCorrect()
        {
            Assert.AreEqual("graphcore/start", StartNodeData.NodeTypeId);
        }

        [Test]
        public void StartNodeData_InheritsBaseNodeData()
        {
            Assert.IsTrue(typeof(BaseNodeData).IsAssignableFrom(typeof(StartNodeData)));
        }

        // T041: StatementNodeData

        [Test]
        public void StatementNodeData_NodeTypeId_IsCorrect()
        {
            Assert.AreEqual("graphcore/statement", StatementNodeData.NodeTypeId);
        }

        [Test]
        public void StatementNodeData_InheritsBaseNodeData()
        {
            Assert.IsTrue(typeof(BaseNodeData).IsAssignableFrom(typeof(StatementNodeData)));
        }

        // T042: ChoiceNodeData

        [Test]
        public void ChoiceNodeData_NodeTypeId_IsCorrect()
        {
            Assert.AreEqual("graphcore/choice", ChoiceNodeData.NodeTypeId);
        }

        [Test]
        public void ChoiceNodeData_Choices_NonNull_OnConstruction()
        {
            var node = new ChoiceNodeData();
            Assert.IsNotNull(node.Choices,
                "ChoiceNodeData.Choices must be non-null on construction.");
        }

        // T043: EndNodeData

        [Test]
        public void EndNodeData_NodeTypeId_IsCorrect()
        {
            Assert.AreEqual("graphcore/end", EndNodeData.NodeTypeId);
        }

        [Test]
        public void EndNodeData_EndReason_DefaultsToCompleted()
        {
            var node = new EndNodeData();
            Assert.AreEqual(EndReason.Completed, node.EndReason,
                "EndReason must default to Completed.");
        }

        // T044: SubGraphNodeData

        [Test]
        public void SubGraphNodeData_NodeTypeId_IsCorrect()
        {
            Assert.AreEqual("graphcore/subgraph", SubGraphNodeData.NodeTypeId);
        }

        [Test]
        public void SubGraphNodeData_TargetGraph_IsNullByDefault()
        {
            var node = new SubGraphNodeData();
            Assert.IsNull(node.TargetGraph, "TargetGraph must be null by default.");
        }

        [Test]
        public void SubGraphNodeData_InheritParentContext_DefaultsFalse()
        {
            var node = new SubGraphNodeData();
            Assert.IsFalse(node.InheritParentContext);
        }
    }
}
