using System.Collections.Generic;
using Faolline.GraphCore;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// A table-less <see cref="ILocalizationProvider"/> that resolves a dialogue's derived line/choice keys to
    /// the authored <c>Title</c> source text. Lets a code-built dialogue (see <see cref="DialogueGraphBuilder"/>)
    /// render its actual text with NO CSV / localization table — otherwise a key with no table entry shows the
    /// bare <c>#line_&lt;guid&gt;</c> marker. For real localization, use the CSV/Unity-Localization providers; this
    /// is the "just show what I authored" path for prototyping and tests.
    /// </summary>
    public sealed class DialogueTitleProvider : ILocalizationProvider
    {
        private readonly Dictionary<string, string> _byKey;

        /// <inheritdoc/>
        public string CurrentLocale { get; }

        private DialogueTitleProvider(Dictionary<string, string> byKey, string locale)
        {
            _byKey = byKey;
            CurrentLocale = string.IsNullOrEmpty(locale) ? "en" : locale;
        }

        /// <inheritdoc/>
        public string Resolve(string key, string locale)
            => (!string.IsNullOrEmpty(key) && _byKey.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                ? v
                : "#" + key;

        /// <summary>
        /// Builds a provider that maps each line/choice key of <paramref name="graph"/> to its authored Title.
        /// </summary>
        public static DialogueTitleProvider FromGraph(DialogueGraph graph, string locale = "en")
        {
            var map = new Dictionary<string, string>();
            if (graph != null)
            {
                foreach (var node in graph.Nodes)
                {
                    if (node is DialogueLineNodeData line && !string.IsNullOrEmpty(line.Title))
                        map[DialogueLocalizationKeys.ForLine(line)] = line.Title;

                    if (node is ChoiceNodeData choiceNode)
                        foreach (var choice in choiceNode.Choices)
                            if (choice != null && !string.IsNullOrEmpty(choice.Title))
                                map[DialogueLocalizationKeys.ForChoice(choice)] = choice.Title;
                }
            }
            return new DialogueTitleProvider(map, locale);
        }
    }
}
