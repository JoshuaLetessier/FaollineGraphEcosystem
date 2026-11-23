using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.StarterGraph;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class StarterStatementNodeDataTests
    {
        [Test]
        public void StarterStatementNodeData_NodeTypeId_IsCorrect()
        {
            Assert.AreEqual("startergraph/statement", StarterStatementNodeData.NodeTypeId,
                "NodeTypeId must be 'startergraph/statement'");
        }

        [Test]
        public void StarterStatementNodeData_IsStatementNodeDataSubclass()
        {
            Assert.IsTrue(
                typeof(StatementNodeData).IsAssignableFrom(typeof(StarterStatementNodeData)),
                "StarterStatementNodeData must extend StatementNodeData");
        }

        [Test]
        public void StarterStatementNodeData_Label_DefaultsToEmpty()
        {
            var node = new StarterStatementNodeData();
            Assert.AreEqual(string.Empty, node.Label,
                "Label must default to empty string");
        }

        [Test]
        public void StarterStatementNodeData_Label_RoundTrips()
        {
            var node = new StarterStatementNodeData();
            node.Label = "Hello, graph!";
            Assert.AreEqual("Hello, graph!", node.Label,
                "Label must round-trip through get/set");
        }

        [Test]
        public void StarterStatementNodeData_Label_AcceptsEmpty()
        {
            var node = new StarterStatementNodeData { Label = "temp" };
            node.Label = string.Empty;
            Assert.AreEqual(string.Empty, node.Label,
                "Label must accept empty string");
        }
    }
}
