using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseActionTests
    {
        [Test]
        public void BaseAction_IsAbstractScriptableObject()
        {
            Assert.IsTrue(typeof(BaseAction).IsAbstract, "BaseAction must be abstract.");
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(typeof(BaseAction)),
                "BaseAction must inherit from ScriptableObject.");
        }

        [Test]
        public void BaseAction_HasExecuteMethod_WithBaseContextParameter()
        {
            var method = typeof(BaseAction).GetMethod("Execute",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "BaseAction must have a public Execute method.");

            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length, "Execute must have exactly one parameter.");
            Assert.AreEqual(typeof(BaseContext), parameters[0].ParameterType,
                "Execute parameter must be BaseContext.");
        }

        [Test]
        public void BaseAction_Execute_IsAbstract()
        {
            var method = typeof(BaseAction).GetMethod("Execute",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(method.IsAbstract, "Execute must be abstract.");
        }
    }
}
