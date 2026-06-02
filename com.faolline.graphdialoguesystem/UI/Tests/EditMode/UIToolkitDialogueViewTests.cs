using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>EditMode tests for UIToolkitDialogueView choice rendering (panel-independent).</summary>
    public class UIToolkitDialogueViewTests
    {
        private GameObject _go;
        private UIToolkitDialogueView _view;
        private VisualElement _root, _container;
        private Label _line, _speaker;

        private UIToolkitDialogueView Build(UIToolkitDialogueView.ChoiceDisplayMode mode, int slotCount = 0)
        {
            _go = new GameObject("uitk-view");
            _view = _go.AddComponent<UIToolkitDialogueView>();
            _root = new VisualElement();
            _line = new Label(); _speaker = new Label();
            _container = new VisualElement();
            _root.Add(_line); _root.Add(_speaker); _root.Add(_container);
            for (int i = 0; i < slotCount; i++)
            {
                var slot = new Button { name = $"choice-{i}" };
                _root.Add(slot);
            }
            _view.ConfigureForTest(_root, _line, _speaker, _container, mode);
            return _view;
        }

        private static ChoiceStep Choices(params (string id, string label, bool available)[] opts)
        {
            var list = new List<ChoiceOption>();
            foreach (var o in opts) list.Add(new ChoiceOption(o.id, o.label, o.available));
            return new ChoiceStep("c", list);
        }

        [TearDown]
        public void TearDown() { if (_go) Object.DestroyImmediate(_go); }

        [Test]
        public void ShowLine_SetsLabels()
        {
            Build(UIToolkitDialogueView.ChoiceDisplayMode.Dynamic);
            _view.ShowLine(new LineStep("l", "npc", "NPC", "Hello", "neutral"));
            Assert.AreEqual("Hello", _line.text);
            Assert.AreEqual("NPC", _speaker.text);
        }

        [Test]
        public void Dynamic_CreatesOneButtonPerOption_WithLabelAndDisabledState()
        {
            Build(UIToolkitDialogueView.ChoiceDisplayMode.Dynamic);
            _view.ShowChoices(Choices(("a", "Option A", true), ("b", "Option B", false)));

            var buttons = new List<Button>();
            foreach (var c in _container.Children()) if (c is Button b) buttons.Add(b);

            Assert.AreEqual(2, buttons.Count);
            Assert.AreEqual("Option A", buttons[0].text);
            Assert.AreEqual("Option B", buttons[1].text);
            Assert.IsTrue(buttons[0].enabledSelf, "Available option enabled.");
            Assert.IsFalse(buttons[1].enabledSelf, "Unavailable option disabled.");
            Assert.IsTrue(buttons[1].ClassListContains("disabled"));
        }

        [Test]
        public void Dynamic_NextStep_ClearsPreviousButtons()
        {
            Build(UIToolkitDialogueView.ChoiceDisplayMode.Dynamic);
            _view.ShowChoices(Choices(("a", "A", true), ("b", "B", true)));
            _view.ShowLine(new LineStep("l", "", "", "next", "neutral"));
            Assert.AreEqual(0, _container.childCount, "Choices cleared when a line follows.");
        }

        [Test]
        public void Slots_PopulatesPresent_HidesAbsent()
        {
            Build(UIToolkitDialogueView.ChoiceDisplayMode.Slots, slotCount: 3);
            _view.ShowChoices(Choices(("a", "Option A", true), ("b", "Option B", false)));

            var s0 = _root.Q<Button>("choice-0");
            var s1 = _root.Q<Button>("choice-1");
            var s2 = _root.Q<Button>("choice-2");

            Assert.AreEqual("Option A", s0.text);
            Assert.AreEqual("Option B", s1.text);
            Assert.IsTrue(s0.enabledSelf);
            Assert.IsFalse(s1.enabledSelf);
            Assert.AreEqual(DisplayStyle.None, s2.style.display.value, "Unused slot hidden.");
        }

        [Test]
        public void HideAll_ClearsLabelsAndChoices()
        {
            Build(UIToolkitDialogueView.ChoiceDisplayMode.Dynamic);
            _view.ShowChoices(Choices(("a", "A", true)));
            _view.HideAll();
            Assert.AreEqual(string.Empty, _line.text);
            Assert.AreEqual(0, _container.childCount);
        }
    }
}
