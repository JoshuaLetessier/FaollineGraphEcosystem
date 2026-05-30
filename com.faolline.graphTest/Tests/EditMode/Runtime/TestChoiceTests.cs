using System;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestChoiceTests
    {
        [Test]
        public void TestChoice_IsBaseChoiceSubclass()
        {
            Assert.IsTrue(
                typeof(BaseChoice).IsAssignableFrom(typeof(TestChoice)),
                "TestChoice must derive from BaseChoice");
        }

        [Test]
        public void TestChoice_HasSerializableAttribute()
        {
            var attrs = typeof(TestChoice).GetCustomAttributes(typeof(SerializableAttribute), inherit: false);
            Assert.IsNotEmpty(attrs, "TestChoice must be [Serializable] for [SerializeReference] storage");
        }

        [Test]
        public void Label_RoundTrips()
        {
            var choice = new TestChoice { Label = "Go left" };
            Assert.AreEqual("Go left", choice.Label);
        }

        [Test]
        public void Label_DefaultsToEmptyNotNull()
        {
            var choice = new TestChoice();
            Assert.AreEqual(string.Empty, choice.Label,
                "Label must default to an empty string, never null");
        }

        [Test]
        public void Label_NullAssignment_CoercesToEmpty()
        {
            var choice = new TestChoice { Label = "x" };
            choice.Label = null;
            Assert.AreEqual(string.Empty, choice.Label,
                "Assigning null to Label must coerce to empty string");
        }

        [Test]
        public void InheritsIdAndCondition()
        {
            var choice = new TestChoice { Id = "abc", Condition = null };
            Assert.AreEqual("abc", choice.Id, "Id is inherited from BaseChoice");
            Assert.IsNull(choice.Condition, "Condition is inherited from BaseChoice and may be null");
        }
    }
}
