using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Faolline.GraphImport
{
    /// <summary>Substitutes <c>{token}</c> placeholders (quest name/id/fields) into a per-kind template string.</summary>
    public sealed class TemplatePathResolver : IPathTemplateResolver
    {
        static readonly Regex TokenPattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        readonly IReadOnlyDictionary<PlanEntryKind, string> _templatesByKind;
        readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _contentFieldsByDialogueId;

        public TemplatePathResolver(IReadOnlyDictionary<PlanEntryKind, string> templatesByKind,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> contentFieldsByDialogueId = null)
        {
            _templatesByKind = templatesByKind;
            _contentFieldsByDialogueId = contentFieldsByDialogueId;
        }

        public string Resolve(PlanEntryKind kind, PivotQuest quest)
        {
            if (!_templatesByKind.TryGetValue(kind, out var template))
                throw new InvalidOperationException($"No path template declared for asset kind '{kind}'.");

            return TokenPattern.Replace(template, match =>
            {
                var token = match.Groups[1].Value;
                if (token == "name") return quest.Name;
                if (token == "id") return quest.Id;
                if (quest.Fields.TryGetValue(token, out var value)) return value;
                throw new InvalidOperationException($"Path template references unknown token '{{{token}}}' — not 'name', 'id', or a mapped field of quest '{quest.Id}'.");
            });
        }

        public string Resolve(PlanEntryKind kind, PivotDialogue dialogue)
        {
            if (!_templatesByKind.TryGetValue(kind, out var template))
                throw new InvalidOperationException($"No path template declared for asset kind '{kind}'.");

            IReadOnlyDictionary<string, string> contentFields = null;
            _contentFieldsByDialogueId?.TryGetValue(dialogue.Id, out contentFields);

            return TokenPattern.Replace(template, match =>
            {
                var token = match.Groups[1].Value;
                if (token == "name") return dialogue.Name;
                if (token == "id") return dialogue.Id;
                if (contentFields != null && contentFields.TryGetValue(token, out var value)) return value;
                throw new InvalidOperationException($"Path template references unknown token '{{{token}}}' — not 'name', 'id', or a content-table field known for dialogue '{dialogue.Id}'.");
            });
        }
    }
}
