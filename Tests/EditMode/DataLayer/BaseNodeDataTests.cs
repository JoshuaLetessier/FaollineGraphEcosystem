using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseNodeDataTests
    {
        private class ConcreteNode : BaseNodeData
        {
            public const string NodeTypeId = "test/concrete";
        }

        // T012: basic structure

        [Test]
        public void BaseNodeData_IsAbstract()
        {
            Assert.IsTrue(typeof(BaseNodeData).IsAbstract, "BaseNodeData must be abstract.");
        }

        [Test]
        public void BaseNodeData_HasRequiredIdentityFields()
        {
            var node = new ConcreteNode
            {
                Id = "id-001",
                NodeType = ConcreteNode.NodeTypeId,
                Position = new Vector2(10f, 20f),
                SerializedPayload = "{\"key\":\"value\"}"
            };
            Assert.AreEqual("id-001", node.Id);
            Assert.AreEqual(ConcreteNode.NodeTypeId, node.NodeType);
            Assert.AreEqual(new Vector2(10f, 20f), node.Position);
            Assert.AreEqual("{\"key\":\"value\"}", node.SerializedPayload);
        }

        // T032-T036: US2 lifecycle hook acceptance criteria

        [Test]
        public void BaseNodeData_EntryConditions_NonNull_OnConstruction()
        {
            var node = new ConcreteNode();
            Assert.IsNotNull(node.EntryConditions,
                "EntryConditions must be non-null on construction.");
        }

        [Test]
        public void BaseNodeData_OnEnterActions_NonNull_OnConstruction()
        {
            var node = new ConcreteNode();
            Assert.IsNotNull(node.OnEnterActions,
                "OnEnterActions must be non-null on construction.");
        }

        [Test]
        public void BaseNodeData_OnExitActions_NonNull_OnConstruction()
        {
            var node = new ConcreteNode();
            Assert.IsNotNull(node.OnExitActions,
                "OnExitActions must be non-null on construction.");
        }

        [Test]
        public void BaseNodeData_HasColorOverride_AndNodeColor_Persist()
        {
            var node = new ConcreteNode
            {
                HasColorOverride = true,
                NodeColor = Color.red
            };
            Assert.IsTrue(node.HasColorOverride);
            Assert.AreEqual(Color.red, node.NodeColor);
        }

        [Test]
        public void BaseNodeData_IsCheckpoint_Persists()
        {
            var node = new ConcreteNode { IsCheckpoint = true };
            Assert.IsTrue(node.IsCheckpoint);
        }
    }
}
