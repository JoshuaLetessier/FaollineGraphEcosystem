using System;
using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    public class NodeExecutorRegistryTests
    {
        // ── Stub executor ─────────────────────────────────────────────────────

        private class StubExecutor : INodeExecutor
        {
            public string NodeType { get; }
            public int ExecuteCalls { get; private set; }
            public int UndoCalls { get; private set; }

            public StubExecutor(string nodeType) => NodeType = nodeType;

            public void Execute(BaseNodeData node, BaseContext context) => ExecuteCalls++;
            public void Undo(BaseNodeData node, BaseContext context) => UndoCalls++;
        }

        // ── Registration / resolution ──────────────────────────────────────────

        [Test]
        public void GetExecutor_RegisteredType_ReturnsExecutor()
        {
            var registry = new NodeExecutorRegistry();
            var executor = new StubExecutor("graphcore/statement");
            registry.Register(executor);

            var resolved = registry.GetExecutor("graphcore/statement");

            Assert.AreSame(executor, resolved);
        }

        [Test]
        public void GetExecutor_UnregisteredType_ReturnsNull()
        {
            var registry = new NodeExecutorRegistry();

            var resolved = registry.GetExecutor("graphcore/unknown");

            Assert.IsNull(resolved);
        }

        [Test]
        public void Register_SameTypeTwice_ReplacesFirst()
        {
            var registry = new NodeExecutorRegistry();
            var first  = new StubExecutor("graphcore/statement");
            var second = new StubExecutor("graphcore/statement");

            registry.Register(first);
            registry.Register(second);

            Assert.AreSame(second, registry.GetExecutor("graphcore/statement"));
        }

        [Test]
        public void Register_NullNodeType_ThrowsArgumentNullException()
        {
            var registry = new NodeExecutorRegistry();
            var badExecutor = new StubExecutor(null);

            Assert.Throws<ArgumentNullException>(() => registry.Register(badExecutor));
        }

        [Test]
        public void Register_MultipleTypes_ResolvesEachCorrectly()
        {
            var registry = new NodeExecutorRegistry();
            var exA = new StubExecutor("graphcore/start");
            var exB = new StubExecutor("graphcore/end");

            registry.Register(exA);
            registry.Register(exB);

            Assert.AreSame(exA, registry.GetExecutor("graphcore/start"));
            Assert.AreSame(exB, registry.GetExecutor("graphcore/end"));
        }

        // ── Default Undo no-op ─────────────────────────────────────────────────

        [Test]
        public void INodeExecutor_DefaultUndo_IsNoOp()
        {
            // StubExecutor overrides Undo, but this test verifies the default path
            // via a minimal inline implementation that doesn't override Undo
            var executor = new MinimalExecutor();
            // Should not throw
            Assert.DoesNotThrow(() => ((INodeExecutor)executor).Undo(null, null));
        }

        // MinimalExecutor only implements required members; Undo uses the default no-op
        private class MinimalExecutor : INodeExecutor
        {
            public string NodeType => "test/minimal";
            public void Execute(BaseNodeData node, BaseContext context) { }
            // Undo deliberately NOT overridden — relies on default interface no-op
        }
    }
}
