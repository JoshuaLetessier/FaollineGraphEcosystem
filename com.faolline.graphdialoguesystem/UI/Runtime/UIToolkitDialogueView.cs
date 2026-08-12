using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphDialogue;
using Faolline.GraphLogging;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// UI Toolkit (UIDocument) dialogue view. Renders the line/speaker into <see cref="Label"/>s and the
    /// choices either by creating one <see cref="Button"/> per option (Dynamic) or by reusing buttons
    /// predefined in the UXML (Slots). Displays already-resolved strings; no localization performed.
    /// </summary>
    public class UIToolkitDialogueView : DialogueViewBase
    {
        /// <summary>How choices are rendered.</summary>
        public enum ChoiceDisplayMode { Dynamic, Slots }

        [Header("UI Document")]
        [SerializeField] private UIDocument document;

        [Header("Element names (UXML)")]
        [SerializeField] private string lineElementName = "line-text";
        [SerializeField] private string speakerElementName = "speaker-name";
        [SerializeField] private string choicesContainerName = "choices-container";

        [Header("Choices")]
        [SerializeField] private ChoiceDisplayMode choiceMode = ChoiceDisplayMode.Dynamic;
        [SerializeField] private string choiceButtonClass = "dialogue-choice";
        [SerializeField] private string choiceSlotPrefix = "choice-";
        [SerializeField] private int maxChoiceSlots = 9;

        private const string DisabledClass = "disabled";

        private VisualElement _root;
        private Label _lineLabel;
        private Label _speakerLabel;
        private VisualElement _choicesContainer;
        private bool _bound;

        private void OnEnable() => _bound = false; // re-bind after enable (document may not be ready in tests)

        // ── Rendering ────────────────────────────────────────────────────────────────

        public override void ShowLine(LineStep step)
        {
            EnsureBound();
            ClearChoices();
            SetText(_speakerLabel, step?.ResolvedSpeakerName);
            if (_speakerLabel != null) _speakerLabel.style.color = ResolveNameColor(step?.SpeakerId);
            ShowText(s => SetText(_lineLabel, s), step?.ResolvedText);

            RequestAvatarSwap(step?.SpeakerId, step?.ExpressionKey);
        }

        public override void ShowChoices(ChoiceStep step)
        {
            EnsureBound();
            SetText(_lineLabel, string.Empty);
            SetText(_speakerLabel, string.Empty);
            ClearChoices();

            var options = step?.Options;
            if (options == null) return;

            if (choiceMode == ChoiceDisplayMode.Slots) RenderSlots(options);
            else RenderDynamic(options);
        }

        public override void HideAll()
        {
            EnsureBound();
            SetText(_lineLabel, string.Empty);
            SetText(_speakerLabel, string.Empty);
            ClearChoices();
            ClearAvatarsOnHide();
        }

        // ── Choice rendering ─────────────────────────────────────────────────────────

        private void RenderDynamic(IReadOnlyList<ChoiceOption> options)
        {
            if (_choicesContainer == null) return;
            foreach (var option in options)
            {
                if (option == null) continue;
                var choiceId = option.ChoiceId;
                var button = new Button(() => RaiseChoiceSelected(choiceId)) { text = option.ResolvedLabel };
                if (!string.IsNullOrEmpty(choiceButtonClass)) button.AddToClassList(choiceButtonClass);
                SetEnabledState(button, option.Available);
                _choicesContainer.Add(button);
            }
        }

        private void RenderSlots(IReadOnlyList<ChoiceOption> options)
        {
            if (_root == null) return;
            int shown = 0;
            for (int i = 0; i < maxChoiceSlots; i++)
            {
                var slot = _root.Q<Button>($"{choiceSlotPrefix}{i}");
                if (slot == null) continue;

                if (i < options.Count && options[i] != null)
                {
                    var option = options[i];
                    var choiceId = option.ChoiceId;
                    slot.text = option.ResolvedLabel;
                    slot.style.display = DisplayStyle.Flex;
                    SetEnabledState(slot, option.Available);
                    slot.clickable = new Clickable(() => RaiseChoiceSelected(choiceId));
                    shown++;
                }
                else
                {
                    slot.style.display = DisplayStyle.None;
                }
            }
            if (options.Count > shown)
                Logging.Warning("GraphDialogue", $"[GraphDialogue] UIToolkitDialogueView: {options.Count} options but only " +
                    $"{shown} slot(s) found with prefix '{choiceSlotPrefix}' — extra options are not shown.");
        }

        private void ClearChoices()
        {
            if (choiceMode == ChoiceDisplayMode.Dynamic)
                _choicesContainer?.Clear();
            else if (_root != null)
                for (int i = 0; i < maxChoiceSlots; i++)
                {
                    var slot = _root.Q<Button>($"{choiceSlotPrefix}{i}");
                    if (slot != null) { slot.text = string.Empty; slot.style.display = DisplayStyle.None; }
                }
        }

        // ── Binding ──────────────────────────────────────────────────────────────────

        private void EnsureBound()
        {
            if (_bound) return;
            if (document == null) return;
            var root = document.rootVisualElement;
            if (root == null) return;
            _root = root;
            _lineLabel = root.Q<Label>(lineElementName);
            _speakerLabel = root.Q<Label>(speakerElementName);
            _choicesContainer = root.Q<VisualElement>(choicesContainerName);
            _bound = true;
        }

        private static void SetText(Label label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        private static void SetEnabledState(Button button, bool available)
        {
            button.SetEnabled(available);
            if (available) button.RemoveFromClassList(DisabledClass);
            else button.AddToClassList(DisabledClass);
        }

        // ── Test seam ──────────────────────────────────────────────────────────────
        // Wires the element tree directly so EditMode tests need no UIDocument/panel.

        internal void ConfigureForTest(VisualElement root, Label line, Label speaker,
            VisualElement choicesContainer, ChoiceDisplayMode mode)
        {
            _root = root;
            _lineLabel = line;
            _speakerLabel = speaker;
            _choicesContainer = choicesContainer;
            choiceMode = mode;
            _bound = true;
        }
    }
}
