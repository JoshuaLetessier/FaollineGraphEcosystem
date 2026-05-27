using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseConditionTests
    {
        [Test]
        public void BaseCondition_IsAbstractScriptableObject()
        {
            Assert.IsTrue(typeof(BaseCondition).IsAbstract, "BaseCondition must be abstract.");
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(typeof(BaseCondition)),
                "BaseCondition must inherit from ScriptableObject.");
        }

        [Test]
        public void BaseCondition_HasEvaluateMethod_ReturningBool()
        {
            var method = typeof(BaseCondition).GetMethod("Evaluate",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "BaseCondition must have a public Evaluate method.");
            Assert.AreEqual(typeof(bool), method.ReturnType, "Evaluate must return bool.");

            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length, "Evaluate must have exactly one parameter.");
            Assert.AreEqual(typeof(BaseContext), parameters[0].ParameterType,
                "Evaluate parameter must be BaseContext.");
        }

        [Test]
        public void BaseCondition_Evaluate_IsAbstract()
        {
            var method = typeof(BaseCondition).GetMethod("Evaluate",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(method.IsAbstract, "Evaluate must be abstract.");
        }
    }
}
