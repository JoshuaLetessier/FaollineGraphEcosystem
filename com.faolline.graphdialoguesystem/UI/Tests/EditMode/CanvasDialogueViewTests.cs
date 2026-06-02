using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using TMPro;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>EditMode tests for CanvasDialogueView rendering and choice click behaviour.</summary>
    public class CanvasDialogueViewTests
    {
        private GameObject _root;
        private CanvasDialogueView _view;
        private TMP_Text _line;
        private TMP_Text _speaker;
        private GameObject _container;
        private List<Button> _buttons;

        private CanvasDialogueView Build(int buttonCount)
        {
            _root = new GameObject("canvas-view");
            _view = _root.AddComponent<CanvasDialogueView>();
            _line = NewText("line");
            _speaker = NewText("speaker");
            _container = new GameObject("choices");
            _buttons = new List<Button>();
            for (int i = 0; i < buttonCount; i++) _buttons.Add(NewButton($"btn{i}"));
            _view.ConfigureForTest(_line, _speaker, _container, _buttons);
            return _view;
        }

        private static TMP_Text NewText(string name)
            => new GameObject(name).AddComponent<TextMeshProUGUI>();

        private static Button NewButton(string name)
        {
            var go = new GameObject(name);
            var btn = go.AddComponent<Button>();
            var label = new GameObject("label").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);
            return btn;
        }

        private static ChoiceStep Choices(params (string id, string label, bool available)[] opts)
        {
            var list = new List<ChoiceOption>();
            foreach (var o in opts) list.Add(new ChoiceOption(o.id, o.label, o.available));
            return new ChoiceStep("c", list);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
            if (_line) Object.DestroyImmediate(_line.gameObject);
            if (_speaker) Object.DestroyImmediate(_speaker.gameObject);
            if (_container) Object.DestroyImmediate(_container);
            if (_buttons != null) foreach (var b in _buttons) if (b) Object.DestroyImmediate(b.gameObject);
        }

        [Test]
        public void ShowLine_SetsTextAndHidesChoices()
        {
            Build(2);
            _view.ShowLine(new LineStep("l", "npc", "NPC", "Hello", "neutral"));

            Assert.AreEqual("Hello", _line.text);
            Assert.AreEqual("NPC", _speaker.text);
            Assert.IsFalse(_container.activeSelf, "Choices hidden during a line.");
        }

        [Test]
        public void ShowChoices_EnablesButtons_WithLabelsAndAvailability()
        {
            Build(3);
            _view.ShowChoices(Choices(("a", "Option A", true), ("b", "Option B", false)));

            Assert.IsTrue(_container.activeSelf);
            Assert.IsTrue(_buttons[0].gameObject.activeSelf);
            Assert.IsTrue(_buttons[1].gameObject.activeSelf);
            Assert.IsFalse(_buttons[2].gameObject.activeSelf, "Unused button hidden.");

            Assert.AreEqual("Option A", _buttons[0].GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Option B", _buttons[1].GetComponentInChildren<TMP_Text>().text);
            Assert.IsTrue(_buttons[0].interactable, "Available option selectable.");
            Assert.IsFalse(_buttons[1].interactable, "Unavailable option not selectable.");
        }

        [Test]
        public void ClickingChoice_RaisesChoiceSelected_WithId()
        {
            Build(2);
            _view.ShowChoices(Choices(("a", "Option A", true), ("b", "Option B", true)));

            string picked = null;
            _view.ChoiceSelected += id => picked = id;
            _buttons[1].onClick.Invoke();

            Assert.AreEqual("b", picked, "Click raises ChoiceSelected with the option's id.");
        }

        [Test]
        public void HideAll_ClearsTextAndButtons()
        {
            Build(2);
            _view.ShowChoices(Choices(("a", "A", true)));
            _view.HideAll();

            Assert.AreEqual(string.Empty, _line.text);
            Assert.AreEqual(string.Empty, _speaker.text);
            Assert.IsFalse(_container.activeSelf);
            Assert.IsFalse(_buttons[0].gameObject.activeSelf);
        }

        [Test]
        public void SurplusOptions_LogsWarning()
        {
            Build(1);
            LogAssert.Expect(LogType.Warning, new Regex("only 1 choice buttons"));
            _view.ShowChoices(Choices(("a", "A", true), ("b", "B", true)));
        }
    }
}
