using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Faolline.GraphImport
{
    /// <summary>Substitutes <c>{token}</c> placeholders (dialogue name/id) into a per-kind template string.</summary>
    public sealed class TemplatePathResolver : IPathTemplateResolver
    {
        static readonly Regex TokenPattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        readonly IReadOnlyDictionary<PlanEntryKind, string> _templatesByKind;

        public TemplatePathResolver(IReadOnlyDictionary<PlanEntryKind, string> templatesByKind)
        {
            _templatesByKind = templatesByKind;
        }

        public string Resolve(PlanEntryKind kind, PivotDialogue dialogue)
        {
            if (!_templatesByKind.TryGetValue(kind, out var template))
                throw new InvalidOperationException($"No path template declared for asset kind '{kind}'.");

            return TokenPattern.Replace(template, match =>
            {
                var token = match.Groups[1].Value;
                if (token == "name") return dialogue.Name;
                if (token == "id") return dialogue.Id;
                throw new InvalidOperationException($"Path template references unknown token '{{{token}}}' — not 'name' or 'id' of dialogue '{dialogue.Id}'.");
            });
        }
    }
}
