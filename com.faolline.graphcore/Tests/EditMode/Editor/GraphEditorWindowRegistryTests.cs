using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>The opt-in graph-type→editor registry resolves a registered type's opener; an unregistered type
    /// or null graph falls back gracefully (select/ping + diagnostic) without throwing.</summary>
    public class GraphEditorWindowRegistryTests
    {
        private sealed class RegisteredFakeGraph : BaseGraph { }
        private sealed class UnregisteredFakeGraph : BaseGraph { }

        [Test]
        public void Open_InvokesRegisteredOpener_ForTheMatchingType()
        {
            var graph = ScriptableObject.CreateInstance<RegisteredFakeGraph>();
            BaseGraph opened = null;
            GraphEditorWindowRegistry.Register(typeof(RegisteredFakeGraph), g => opened = g);
            try
            {
                GraphEditorWindowRegistry.Open(graph);
                Assert.AreSame(graph, opened, "the registered opener is invoked with the graph.");
            }
            finally { GraphEditorWindowRegistry.Clear(); Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Open_UnregisteredType_FallsBackGracefully_NoThrow()
        {
            var graph = ScriptableObject.CreateInstance<UnregisteredFakeGraph>();
            try
            {
                Assert.DoesNotThrow(() => GraphEditorWindowRegistry.Open(graph),
                    "an unregistered type falls back to selecting/pinging the asset, never throws.");
            }
            finally { GraphEditorWindowRegistry.Clear(); Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Open_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => GraphEditorWindowRegistry.Open(null));
            GraphEditorWindowRegistry.Clear();
        }
    }
}
