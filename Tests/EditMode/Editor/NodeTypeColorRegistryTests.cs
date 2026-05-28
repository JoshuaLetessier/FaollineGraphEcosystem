using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    [TestFixture]
    public class NodeTypeColorRegistryTests
    {
        [TearDown]
        public void TearDown()
        {
            NodeTypeColorRegistry.Clear();
        }

        [Test]
        public void Register_StoresColor()
        {
            NodeTypeColorRegistry.Register("test/node", Color.red);
            Assert.IsTrue(NodeTypeColorRegistry.TryGet("test/node", out var color));
            Assert.AreEqual(Color.red, color);
        }

        [Test]
        public void Register_Twice_SecondReplaceFirst()
        {
            NodeTypeColorRegistry.Register("test/node", Color.red);
            NodeTypeColorRegistry.Register("test/node", Color.blue);
            Assert.IsTrue(NodeTypeColorRegistry.TryGet("test/node", out var color));
            Assert.AreEqual(Color.blue, color);
        }

        [Test]
        public void TryGet_UnknownType_ReturnsFalse()
        {
            Assert.IsFalse(NodeTypeColorRegistry.TryGet("unknown/type", out _));
        }

        [Test]
        public void Clear_ResetsAllRegistrations()
        {
            NodeTypeColorRegistry.Register("test/a", Color.red);
            NodeTypeColorRegistry.Register("test/b", Color.green);
            NodeTypeColorRegistry.Clear();
            Assert.IsFalse(NodeTypeColorRegistry.TryGet("test/a", out _));
            Assert.IsFalse(NodeTypeColorRegistry.TryGet("test/b", out _));
        }
    }
}
