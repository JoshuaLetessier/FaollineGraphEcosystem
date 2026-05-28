using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseEdgeDataTests
    {
        [Test]
        public void BaseEdgeData_IsSerializable()
        {
            var attrs = typeof(BaseEdgeData).GetCustomAttributes(
                typeof(System.SerializableAttribute), false);
            Assert.IsTrue(attrs.Length > 0, "BaseEdgeData must be marked [Serializable].");
        }

        [Test]
        public void BaseEdgeData_HasRequiredFields()
        {
            var edge = new BaseEdgeData
            {
                Id = "edge-001",
                FromNodeId = "node-a",
                ToNodeId = "node-b",
                PortName = "out",
                HasColorOverride = true,
                EdgeColor = Color.green
            };
            Assert.AreEqual("edge-001", edge.Id);
            Assert.AreEqual("node-a", edge.FromNodeId);
            Assert.AreEqual("node-b", edge.ToNodeId);
            Assert.AreEqual("out", edge.PortName);
            Assert.IsTrue(edge.HasColorOverride);
            Assert.AreEqual(Color.green, edge.EdgeColor);
        }

        [Test]
        public void BaseEdgeData_Condition_IsNullByDefault()
        {
            var edge = new BaseEdgeData();
            Assert.IsNull(edge.Condition, "Condition must be null by default.");
        }
    }
}
