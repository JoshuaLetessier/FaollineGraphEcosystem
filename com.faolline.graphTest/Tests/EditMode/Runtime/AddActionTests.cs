using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class AddActionTests
    {
        private BaseContext _context;

        [SetUp]
        public void SetUp() => _context = new BaseContext();

        [Test]
        public void AddIntAction_AddsToExistingValue()
        {
            _context.Set<int>("gold", 100);
            var action = ScriptableObject.CreateInstance<AddIntAction>();
            action.ParameterKey = "gold"; action.Value = 50;
            try
            {
                action.Execute(_context);
                Assert.IsTrue(_context.TryGet<int>("gold", out var v));
                Assert.AreEqual(150, v);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void AddIntAction_DefaultsToZeroWhenAbsent()
        {
            var action = ScriptableObject.CreateInstance<AddIntAction>();
            action.ParameterKey = "xp"; action.Value = 25;
            try
            {
                action.Execute(_context);
                Assert.IsTrue(_context.TryGet<int>("xp", out var v));
                Assert.AreEqual(25, v);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void AddIntAction_NegativeSubtracts()
        {
            _context.Set<int>("gold", 100);
            var action = ScriptableObject.CreateInstance<AddIntAction>();
            action.ParameterKey = "gold"; action.Value = -30;
            try
            {
                action.Execute(_context);
                Assert.IsTrue(_context.TryGet<int>("gold", out var v));
                Assert.AreEqual(70, v);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void AddFloatAction_AddsToExistingValue()
        {
            _context.Set<float>("hp", 1f);
            var action = ScriptableObject.CreateInstance<AddFloatAction>();
            action.ParameterKey = "hp"; action.Value = -0.25f;
            try
            {
                action.Execute(_context);
                Assert.IsTrue(_context.TryGet<float>("hp", out var v));
                Assert.AreEqual(0.75f, v, 0.0001f);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void AddFloatAction_DefaultsToZeroWhenAbsent()
        {
            var action = ScriptableObject.CreateInstance<AddFloatAction>();
            action.ParameterKey = "progress"; action.Value = 0.1f;
            try
            {
                action.Execute(_context);
                Assert.IsTrue(_context.TryGet<float>("progress", out var v));
                Assert.AreEqual(0.1f, v, 0.0001f);
            }
            finally { Object.DestroyImmediate(action); }
        }
    }
}
