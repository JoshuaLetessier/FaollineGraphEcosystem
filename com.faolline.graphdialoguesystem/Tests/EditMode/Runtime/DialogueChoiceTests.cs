using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the dialogue choice type.</summary>
    public class DialogueChoiceTests
    {
        [Test]
        public void IsBaseChoice_WithDisplayTextKey()
        {
            var choice = new DialogueChoice { Id = "a", DisplayTextKey = "dlg.yes" };
            Assert.IsInstanceOf<BaseChoice>(choice);
            Assert.AreEqual("a", choice.Id);
            Assert.AreEqual("dlg.yes", choice.DisplayTextKey);
            Assert.IsNull(choice.Condition);
        }

        [Test]
        public void DisplayTextKey_NeverNull()
        {
            var choice = new DialogueChoice { DisplayTextKey = null };
            Assert.AreEqual(string.Empty, choice.DisplayTextKey);
        }
    }
}
