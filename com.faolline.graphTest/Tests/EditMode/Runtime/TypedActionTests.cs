using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TypedActionTests
    {
        [Test]
        public void SetIntAction_WritesValue()
        {
            var action = ScriptableObject.CreateInstance<TestSetIntAction>();
            action.ParameterKey = "score"; action.Value = 7;
            var ctx = new BaseContext();
            try
            {
                action.Execute(ctx);
                Assert.IsTrue(ctx.TryGet<int>("score", out var v));
                Assert.AreEqual(7, v);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void SetFloatAction_WritesValue()
        {
            var action = ScriptableObject.CreateInstance<TestSetFloatAction>();
            action.ParameterKey = "hp"; action.Value = 0.25f;
            var ctx = new BaseContext();
            try
            {
                action.Execute(ctx);
                Assert.IsTrue(ctx.TryGet<float>("hp", out var v));
                Assert.AreEqual(0.25f, v, 0.0001f);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void SetStringAction_WritesValue()
        {
            var action = ScriptableObject.CreateInstance<TestSetStringAction>();
            action.ParameterKey = "name"; action.Value = "hero";
            var ctx = new BaseContext();
            try
            {
                action.Execute(ctx);
                Assert.IsTrue(ctx.TryGet<string>("name", out var v));
                Assert.AreEqual("hero", v);
            }
            finally { Object.DestroyImmediate(action); }
        }
    }
}
