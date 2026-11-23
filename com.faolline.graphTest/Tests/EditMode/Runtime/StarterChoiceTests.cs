using System;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.StarterGraph;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class StarterChoiceTests
    {
        [Test]
        public void StarterChoice_IsBaseChoiceSubclass()
        {
            Assert.IsTrue(
                typeof(BaseChoice).IsAssignableFrom(typeof(StarterChoice)),
                "StarterChoice must derive from BaseChoice");
        }

        [Test]
        public void StarterChoice_HasSerializableAttribute()
        {
            var attrs = typeof(StarterChoice).GetCustomAttributes(typeof(SerializableAttribute), inherit: false);
            Assert.IsNotEmpty(attrs, "StarterChoice must be [Serializable] for [SerializeReference] storage");
        }

        [Test]
        public void Label_RoundTrips()
        {
            var choice = new StarterChoice { Label = "Go left" };
            Assert.AreEqual("Go left", choice.Label);
        }

        [Test]
        public void Label_DefaultsToEmptyNotNull()
        {
            var choice = new StarterChoice();
            Assert.AreEqual(string.Empty, choice.Label,
                "Label must default to an empty string, never null");
        }

        [Test]
        public void Label_NullAssignment_CoercesToEmpty()
        {
            var choice = new StarterChoice { Label = "x" };
            choice.Label = null;
            Assert.AreEqual(string.Empty, choice.Label,
                "Assigning null to Label must coerce to empty string");
        }

        [Test]
        public void InheritsIdAndCondition()
        {
            var choice = new StarterChoice { Id = "abc", Condition = null };
            Assert.AreEqual("abc", choice.Id, "Id is inherited from BaseChoice");
            Assert.IsNull(choice.Condition, "Condition is inherited from BaseChoice and may be null");
        }
    }
}
