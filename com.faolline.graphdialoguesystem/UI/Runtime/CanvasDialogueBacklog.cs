using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Simple Canvas (TextMeshPro) backlog/history panel. Lists the lines shown by a
    /// <see cref="DialogueDriver"/> by cloning a text template per entry. Subscribes to the driver's
    /// <see cref="DialogueDriver.OnLineShown"/> and rebuilds from <see cref="DialogueDriver.History"/> on
    /// bind. A UI Toolkit backlog can be built the same way from those two members.
    /// </summary>
    public class CanvasDialogueBacklog : MonoBehaviour
    {
        [SerializeField] private DialogueDriver driver;
        [SerializeField, Tooltip("Vertical layout container the entries are added to.")]
        private RectTransform content;
        [SerializeField, Tooltip("Disabled TMP text cloned once per backlog entry.")]
        private TMP_Text entryTemplate;
        [SerializeField, Tooltip("Optional panel root toggled by Show/Hide/Toggle.")]
        private GameObject panel;
        [SerializeField, Tooltip("{0} = speaker name, {1} = line text.")]
        private string entryFormat = "{0}: {1}";

        private readonly List<GameObject> _entries = new List<GameObject>();

        /// <summary>Number of backlog entries currently listed.</summary>
        public int EntryCount => _entries.Count;

        private void OnEnable() { if (driver != null) Bind(driver); }
        private void OnDisable() { if (driver != null) driver.OnLineShown -= AddEntry; }

        /// <summary>Subscribes to <paramref name="target"/> and rebuilds the list from its current history.</summary>
        public void Bind(DialogueDriver target)
        {
            if (driver != null) driver.OnLineShown -= AddEntry;
            driver = target;
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
            Rebuild();
            if (driver != null) driver.OnLineShown += AddEntry;
        }

        /// <summary>Clears and re-adds every entry from the driver's history.</summary>
        public void Rebuild()
        {
            Clear();
            if (driver == null) return;
            foreach (var line in driver.History) AddEntry(line);
        }

        public void Clear()
        {
            foreach (var e in _entries) DestroyEntry(e);
            _entries.Clear();
        }

        public void Show() { if (panel != null) panel.SetActive(true); }
        public void Hide() { if (panel != null) panel.SetActive(false); }
        public void Toggle() { if (panel != null) panel.SetActive(!panel.activeSelf); }

        private void AddEntry(LineStep step)
        {
            if (entryTemplate == null || content == null || step == null) return;
            var go = Instantiate(entryTemplate.gameObject, content);
            go.SetActive(true);
            var label = go.GetComponent<TMP_Text>();
            if (label != null) label.text = string.Format(entryFormat, step.ResolvedSpeakerName, step.ResolvedText);
            _entries.Add(go);
        }

        private static void DestroyEntry(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        internal void ConfigureForTest(DialogueDriver target, RectTransform contentRoot, TMP_Text template)
        {
            content = contentRoot;
            entryTemplate = template;
            Bind(target);
        }
    }
}
