using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Canvas (UGUI + TextMeshPro) dialogue view. Renders the line/speaker into <see cref="TMP_Text"/>
    /// fields and the choices onto a fixed set of <see cref="Button"/>s. Displays already-resolved
    /// strings; performs no localization. Choice clicks raise <see cref="IDialogueView.ChoiceSelected"/>.
    /// </summary>
    public class CanvasDialogueView : DialogueViewBase
    {
        [Header("Text")]
        [SerializeField] private TMP_Text lineText;
        [SerializeField] private TMP_Text speakerText;

        [Header("Choices")]
        [SerializeField] private GameObject choicesContainer;
        [SerializeField] private List<Button> choiceButtons = new List<Button>();

        // ── Rendering ────────────────────────────────────────────────────────────────

        public override void ShowLine(LineStep step)
        {
            if (choicesContainer) choicesContainer.SetActive(false);
            DeactivateAllChoices();

            SetText(lineText, step?.ResolvedText);
            SetText(speakerText, step?.ResolvedSpeakerName);

            RequestAvatarSwap(step?.SpeakerId, step?.ExpressionKey);
        }

        public override void ShowChoices(ChoiceStep step)
        {
            SetText(lineText, string.Empty);
            SetText(speakerText, string.Empty);

            if (choicesContainer) choicesContainer.SetActive(true);
            DeactivateAllChoices();

            var options = step?.Options;
            if (options == null) return;

            if (options.Count > choiceButtons.Count)
                Debug.LogWarning($"[GraphDialogue] CanvasDialogueView: {options.Count} options but only " +
                    $"{choiceButtons.Count} choice buttons — extra options are not shown.");

            for (int i = 0; i < choiceButtons.Count && i < options.Count; i++)
            {
                var button = choiceButtons[i];
                var option = options[i];
                if (button == null || option == null) continue;

                button.gameObject.SetActive(true);
                button.interactable = option.Available;
                SetText(button.GetComponentInChildren<TMP_Text>(), option.ResolvedLabel);

                var choiceId = option.ChoiceId;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => RaiseChoiceSelected(choiceId));
            }
        }

        public override void HideAll()
        {
            if (choicesContainer) choicesContainer.SetActive(false);
            SetText(lineText, string.Empty);
            SetText(speakerText, string.Empty);
            DeactivateAllChoices();
            ClearAvatarsOnHide();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private void DeactivateAllChoices()
        {
            foreach (var b in choiceButtons)
            {
                if (b == null) continue;
                b.onClick.RemoveAllListeners();
                if (b.gameObject.activeSelf) b.gameObject.SetActive(false);
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        // ── Test seam ──────────────────────────────────────────────────────────────
        // Lets EditMode tests wire fields without a prefab.

        internal void ConfigureForTest(TMP_Text line, TMP_Text speaker, GameObject container, List<Button> buttons)
        {
            lineText = line;
            speakerText = speaker;
            choicesContainer = container;
            choiceButtons = buttons ?? new List<Button>();
        }
    }
}
