using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using SetBoolAction = Faolline.GraphDialogue.SetBoolAction;   // disambiguate from Faolline.GraphCore.SetBoolAction

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
            var a = Make<SetBoolAction>();
            a.ParameterKey = "k"; a.Value = true;
            a.Execute(ctx);
            Assert.IsTrue(ctx.Get<bool>("k"));
        }

        [Test]
        public void SetInt_Writes()
        {
            var ctx = new DialogueContext();
            var a = Make<SetIntAction>();
            a.ParameterKey = "k"; a.Value = 42;
            a.Execute(ctx);
            Assert.AreEqual(42, ctx.Get<int>("k"));
        }

        [Test]
        public void SetFloat_Writes()
        {
            var ctx = new DialogueContext();
            var a = Make<SetFloatAction>();
            a.ParameterKey = "k"; a.Value = 1.25f;
            a.Execute(ctx);
            Assert.AreEqual(1.25f, ctx.Get<float>("k"));
        }

        [Test]
        public void SetString_Writes()
        {
            var ctx = new DialogueContext();
            var a = Make<SetStringAction>();
            a.ParameterKey = "k"; a.Value = "v";
            a.Execute(ctx);
            Assert.AreEqual("v", ctx.Get<string>("k"));
        }
    }
}
