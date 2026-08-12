using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Real V1 implementation of the shared asset-resolution seam.
    ///
    /// <see cref="ResolveGraph"/> only ever resolves a target that is itself part of the
    /// <see cref="GenerationPlan"/> this resolver was built from — it never searches the wider
    /// project or guesses a path. This means it resolves cleanly for a sub-dialogue link (or a
    /// quest step's content ref) whose target is being generated in the SAME run; a target that
    /// was generated in an earlier run, or hand-authored outside this pipeline, still resolves to
    /// null (the documented-safe "incomplete node" state), consistent with the rest of the
    /// pipeline's "never guess" precedent. Callers who want cross-resolution between quest and
    /// dialogue generation in one run should build the resolver from the UNION of both plans'
    /// entries.
    ///
    /// <see cref="ResolveSpeaker"/> is find-or-create: reuses an existing <see cref="Speaker"/>
    /// asset with a matching <see cref="Speaker.SpeakerId"/> anywhere under Assets, or creates one
    /// under <c>speakerFolder</c> if none exists. Never creates a duplicate for the same key,
    /// including across repeated calls on the same resolver instance.
    ///
    /// A newly-created Speaker's folder can optionally be routed by a mapping-declared "content"
    /// table too (same mechanism as <see cref="TemplatePathResolver"/>'s dialogue overload — see
    /// <see cref="PivotBuilder.BuildContentFields"/>): pass <c>speakerFolderTemplate</c> (e.g.
    /// <c>"Content/{chapter}/Graph/Speakers"</c>) plus the same content-fields lookup keyed by
    /// speaker key, and <c>speakerFolder</c> becomes unused for creation (kept only as the
    /// unambiguous default when no template is given, so every existing caller/test is unaffected).
    /// </summary>
    public sealed class ProjectAssetResolver : IProjectAssetResolver
    {
        static readonly Regex TokenPattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        readonly IReadOnlyDictionary<string, string> _pathsByPivotId;
        readonly string _speakerFolder;
        readonly string _speakerFolderTemplate;
        readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _contentFieldsBySpeakerKey;
        readonly Dictionary<string, Speaker> _speakerCache = new Dictionary<string, Speaker>();

        public ProjectAssetResolver(GenerationPlan plan, string speakerFolder,
            string speakerFolderTemplate = null,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> contentFieldsBySpeakerKey = null)
        {
            _pathsByPivotId = BuildPathsByPivotId(plan);
            _speakerFolder = speakerFolder;
            _speakerFolderTemplate = speakerFolderTemplate;
            _contentFieldsBySpeakerKey = contentFieldsBySpeakerKey;
        }

        /// <summary>
        /// A plan can legitimately contain more than one entry sharing a SourcePivotId — a quest with
        /// steps gets both a QuestAsset and a FlowAsset entry, both keyed by the same quest.Id. Nothing
        /// ever resolves a reference TO a quest/flow asset by id (only content refs and sub-dialogue
        /// links are ever looked up), so first-entry-wins on a duplicate id is both crash-safe and
        /// correct for every reference kind this resolver is actually asked to resolve.
        /// </summary>
        static Dictionary<string, string> BuildPathsByPivotId(GenerationPlan plan)
        {
            var paths = new Dictionary<string, string>();
            foreach (var entry in plan.Entries)
                if (!paths.ContainsKey(entry.SourcePivotId))
                    paths[entry.SourcePivotId] = entry.ProposedPath;
            return paths;
        }

        public BaseGraph ResolveGraph(string targetTable, string targetId)
        {
            return _pathsByPivotId.TryGetValue(targetId, out var path)
                ? AssetDatabase.LoadAssetAtPath<BaseGraph>(path)
                : null;
        }

        public Speaker ResolveSpeaker(string speakerKey)
        {
            if (string.IsNullOrEmpty(speakerKey))
                return null;

            if (_speakerCache.TryGetValue(speakerKey, out var cached))
                return cached;

            var existing = FindExistingSpeaker(speakerKey);
            var speaker = existing != null ? existing : CreateSpeaker(speakerKey);

            _speakerCache[speakerKey] = speaker;
            return speaker;
        }

        static Speaker FindExistingSpeaker(string speakerKey)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Speaker"))
            {
                var speaker = AssetDatabase.LoadAssetAtPath<Speaker>(AssetDatabase.GUIDToAssetPath(guid));
                if (speaker != null && speaker.SpeakerId == speakerKey)
                    return speaker;
            }
            return null;
        }

        Speaker CreateSpeaker(string speakerKey)
        {
            var folder = ResolveSpeakerFolder(speakerKey);
            EnsureFolderExists(folder);

            var speaker = ScriptableObject.CreateInstance<Speaker>();
            speaker.SpeakerId = speakerKey;
            speaker.DisplayNameFallback = speakerKey;

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SanitizeFileName(speakerKey)}.asset");
            AssetDatabase.CreateAsset(speaker, path);
            return speaker;
        }

        string ResolveSpeakerFolder(string speakerKey)
        {
            if (_speakerFolderTemplate == null)
                return _speakerFolder;

            IReadOnlyDictionary<string, string> contentFields = null;
            _contentFieldsBySpeakerKey?.TryGetValue(speakerKey, out contentFields);

            return TokenPattern.Replace(_speakerFolderTemplate, match =>
            {
                var token = match.Groups[1].Value;
                if (token == "speakerKey") return speakerKey;
                if (contentFields != null && contentFields.TryGetValue(token, out var value)) return value;
                throw new InvalidOperationException($"Speaker folder template references unknown token '{{{token}}}' — not 'speakerKey', or a content-table field known for speaker '{speakerKey}'.");
            });
        }

        static string SanitizeFileName(string raw)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = raw.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }

        static void EnsureFolderExists(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
