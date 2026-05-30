using NUnit.Framework;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestStatementNodeDataTests
    {
        [Test]
        public void TestStatementNodeData_NodeTypeId_IsCorrect()
        {
            Assert.AreEqual("graphtest/statement", TestStatementNodeData.NodeTypeId,
                "NodeTypeId must be 'graphtest/statement'");
        }

        [Test]
        public void TestStatementNodeData_IsStatementNodeDataSubclass()
        {
            Assert.IsTrue(
                typeof(StatementNodeData).IsAssignableFrom(typeof(TestStatementNodeData)),
                "TestStatementNodeData must extend StatementNodeData");
        }

        [Test]
        public void TestStatementNodeData_Label_DefaultsToEmpty()
        {
            var node = new TestStatementNodeData();
            Assert.AreEqual(string.Empty, node.Label,
                "Label must default to empty string");
        }

        [Test]
        public void TestStatementNodeData_Label_RoundTrips()
        {
            var node = new TestStatementNodeData();
            node.Label = "Hello, graph!";
            Assert.AreEqual("Hello, graph!", node.Label,
                "Label must round-trip through get/set");
        }

        [Test]
        public void TestStatementNodeData_Label_AcceptsEmpty()
        {
            var node = new TestStatementNodeData { Label = "temp" };
            node.Label = string.Empty;
            Assert.AreEqual(string.Empty, node.Label,
                "Label must accept empty string");
        }
    }
}
