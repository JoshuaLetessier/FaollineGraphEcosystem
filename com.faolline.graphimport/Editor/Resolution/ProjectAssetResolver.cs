using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// </summary>
    public sealed class ProjectAssetResolver : IProjectAssetResolver
    {
        readonly IReadOnlyDictionary<string, string> _pathsByPivotId;
        readonly string _speakerFolder;
        readonly Dictionary<string, Speaker> _speakerCache = new Dictionary<string, Speaker>();

        public ProjectAssetResolver(GenerationPlan plan, string speakerFolder)
        {
            _pathsByPivotId = plan.Entries.ToDictionary(e => e.SourcePivotId, e => e.ProposedPath);
            _speakerFolder = speakerFolder;
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
            EnsureFolderExists(_speakerFolder);

            var speaker = ScriptableObject.CreateInstance<Speaker>();
            speaker.SpeakerId = speakerKey;
            speaker.DisplayNameFallback = speakerKey;

            var path = AssetDatabase.GenerateUniqueAssetPath($"{_speakerFolder}/{SanitizeFileName(speakerKey)}.asset");
            AssetDatabase.CreateAsset(speaker, path);
            return speaker;
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
