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
            var k = ParameterName.Bool("k");
            var a = Make<SetBoolAction>();
            a.Parameter = k; a.Value = true;
            a.Execute(ctx);
            Assert.IsTrue(ctx.Get<bool>(k));
        }

        [Test]
        public void SetInt_Writes()
        {
            var ctx = new DialogueContext();
            var k = ParameterName.Int("k");
            var a = Make<SetIntAction>();
            a.Parameter = k; a.Value = 42;
            a.Execute(ctx);
            Assert.AreEqual(42, ctx.Get<int>(k));
        }

        [Test]
        public void SetFloat_Writes()
        {
            var ctx = new DialogueContext();
            var k = ParameterName.Float("k");
            var a = Make<SetFloatAction>();
            a.Parameter = k; a.Value = 1.25f;
            a.Execute(ctx);
            Assert.AreEqual(1.25f, ctx.Get<float>(k));
        }

        [Test]
        public void SetString_Writes()
        {
            var ctx = new DialogueContext();
            var k = ParameterName.String("k");
            var a = Make<SetStringAction>();
            a.Parameter = k; a.Value = "v";
            a.Execute(ctx);
            Assert.AreEqual("v", ctx.Get<string>(k));
        }
    }
}
