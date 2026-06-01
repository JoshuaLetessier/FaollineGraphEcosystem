using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the dialogue choice type.</summary>
    public class DialogueChoiceTests
    {
        [Test]
        public void IsBaseChoice_CarriesIdTitleCondition()
        {
            var choice = new DialogueChoice { Id = "a", Title = "Ask" };
            Assert.IsInstanceOf<BaseChoice>(choice);
            Assert.AreEqual("a", choice.Id);
            Assert.AreEqual("Ask", choice.Title);
            Assert.IsNull(choice.Condition);
        }

        [Test]
        public void LocalizationKey_IsDerivedFromId()
        {
            var choice = new DialogueChoice { Id = "a" };
            // The label's localization key is derived from the choice Id, not a stored field.
            Assert.AreEqual("choice_a", DialogueLocalizationKeys.ForChoice(choice));
        }
    }
}
