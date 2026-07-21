using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// <see cref="SceneAwaitSetup.ConfigureLoadAwait"/> collapses the three separate settings a load-await
    /// node needs (<c>AwaitSignalName</c>, <c>AwaitSignalNamesExtra</c>, <c>ResumeIfSignalAlreadyRaised</c>)
    /// into one call. Pure data manipulation — no scene, no loader, no coroutine involved.
    /// </summary>
    public class SceneAwaitSetupTests
    {
        private static SignalDef Sig(string name) => SignalDef.Create(name);
        private static StatementNodeData Node(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };

        [Test]
        public void ConfigureLoadAwait_SetsPrimaryName_ExtraName_AndResumeFlag()
        {
            var node = Node("gate");
            var completed = Sig("loaded");
            var failed = Sig("load-failed");

            SceneAwaitSetup.ConfigureLoadAwait(node, completed, failed);

            Assert.AreEqual((string)completed, node.AwaitSignalName, "the completed signal becomes the primary await name.");
            CollectionAssert.Contains(node.AwaitSignalNamesExtra, (string)failed, "the failed signal is appended as an OR-await extra.");
            Assert.IsTrue(node.ResumeIfSignalAlreadyRaised, "on by default, so an instant failure/completion that already fired is still caught.");
        }

        [Test]
        public void ConfigureLoadAwait_ResumeIfAlreadyRaisedCanBeOptedOut()
        {
            var node = Node("gate");
            SceneAwaitSetup.ConfigureLoadAwait(node, Sig("loaded"), Sig("load-failed"), resumeIfAlreadyRaised: false);

            Assert.IsFalse(node.ResumeIfSignalAlreadyRaised);
        }

        [Test]
        public void ConfigureLoadAwait_FailedSignalIsOptional()
        {
            var node = Node("gate");
            var completed = Sig("loaded");

            SceneAwaitSetup.ConfigureLoadAwait(node, completed);

            Assert.AreEqual((string)completed, node.AwaitSignalName);
            Assert.AreEqual(0, node.AwaitSignalNamesExtra.Count, "no failed signal was given, so nothing is appended.");
        }

        [Test]
        public void ConfigureLoadAwait_NullNode_LogsErrorNoThrow()
        {
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] SceneAwaitSetup.ConfigureLoadAwait called with a null node; ignored.");
            Assert.DoesNotThrow(() => SceneAwaitSetup.ConfigureLoadAwait(null, Sig("loaded")));
        }

        [Test]
        public void ConfigureLoadAwait_NullCompletedSignal_LogsErrorNoThrow()
        {
            var node = Node("gate");
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] SceneAwaitSetup.ConfigureLoadAwait called with a null completedSignal; ignored.");
            Assert.DoesNotThrow(() => SceneAwaitSetup.ConfigureLoadAwait(node, null));
            Assert.IsTrue(string.IsNullOrEmpty(node.AwaitSignalName), "an invalid call must not partially configure the node.");
        }
    }
}
