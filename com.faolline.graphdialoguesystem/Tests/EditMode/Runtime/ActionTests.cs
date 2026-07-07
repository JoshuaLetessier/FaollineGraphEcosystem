using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the inline effect (action) set.</summary>
    public class ActionTests
    {
        private static T Make<T>() where T : ScriptableObject => ScriptableObject.CreateInstance<T>();

        [Test]
        public void SetBool_Writes()
        {
            var ctx = new DialogueContext();
            var k = VariableDef.Bool("k");
            var a = Make<SetBoolAction>();
            a.Variable = k; a.Value = true;
            a.Execute(ctx);
            Assert.IsTrue(ctx.Get<bool>(k));
        }

        [Test]
        public void SetInt_Writes()
        {
            var ctx = new DialogueContext();
            var k = VariableDef.Int("k");
            var a = Make<SetIntAction>();
            a.Variable = k; a.Value = 42;
            a.Execute(ctx);
            Assert.AreEqual(42, ctx.Get<int>(k));
        }

        [Test]
        public void SetFloat_Writes()
        {
            var ctx = new DialogueContext();
            var k = VariableDef.Float("k");
            var a = Make<SetFloatAction>();
            a.Variable = k; a.Value = 1.25f;
            a.Execute(ctx);
            Assert.AreEqual(1.25f, ctx.Get<float>(k));
        }

        [Test]
        public void SetString_Writes()
        {
            var ctx = new DialogueContext();
            var k = VariableDef.String("k");
            var a = Make<SetStringAction>();
            a.Variable = k; a.Value = "v";
            a.Execute(ctx);
            Assert.AreEqual("v", ctx.Get<string>(k));
        }
    }
}
