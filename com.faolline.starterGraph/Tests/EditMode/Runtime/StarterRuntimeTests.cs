using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;

namespace Faolline.StarterGraph.Tests
{
    /// <summary>The template's runtime extension points: graph type, choice, the example node/action/condition.</summary>
    [TestFixture]
    public class StarterRuntimeTests
    {
        [Test]
        public void StarterGraph_IsBaseGraph_WithCreateAssetMenu()
        {
            Assert.IsTrue(typeof(BaseGraph).IsAssignableFrom(typeof(StarterGraph)));
            Assert.IsNotEmpty(typeof(StarterGraph).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false));
        }

        [Test]
        public void StarterChoice_IsBaseChoice_Serializable_WithLabel()
        {
            Assert.IsTrue(typeof(BaseChoice).IsAssignableFrom(typeof(StarterChoice)));
            Assert.IsNotEmpty(typeof(StarterChoice).GetCustomAttributes(typeof(SerializableAttribute), false));
            var c = new StarterChoice { Id = "x", Label = "Go" };
            Assert.AreEqual("Go", c.Label);
            Assert.AreEqual("x", c.Id);
        }

        [Test]
        public void StarterStatementNode_HasLabel_AndTypeId()
        {
            var n = new StarterStatementNodeData { Label = "Hi" };
            Assert.AreEqual("Hi", n.Label);
            Assert.AreEqual("startergraph/statement", StarterStatementNodeData.NodeTypeId);
        }

        [Test]
        public void CoreLogAction_Executes_WithoutError()
        {
            var a = ScriptableObject.CreateInstance<LogAction>();
            a.Message = "hello";
            try
            {
                LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("hello"));
                a.Execute(new BaseContext());
            }
            finally { UnityEngine.Object.DestroyImmediate(a); }
        }

        [Test]
        public void CoreBoolCondition_ReadsTypedContextKey()
        {
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.ParameterKey = StarterContextKeys.Flag; c.ExpectedValue = true;
            var ctx = new StarterContext { Flag = true };
            try
            {
                Assert.IsTrue(c.Evaluate(ctx));
                ctx.Flag = false;
                Assert.IsFalse(c.Evaluate(ctx));
            }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }
    }
}
