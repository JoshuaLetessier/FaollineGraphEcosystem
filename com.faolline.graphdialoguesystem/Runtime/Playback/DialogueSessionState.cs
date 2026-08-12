using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Faolline.GraphCore;
using UnityEngine;
using Faolline.GraphLogging;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Serializable snapshot of a dialogue session: the current node and the context values.
    /// Persist via <see cref="ToJson"/> / <see cref="FromJson"/>; restore via
    /// <see cref="DialoguePlayer.RestoreFrom"/>.
    ///
    /// Design: checkpoint-level granularity. <see cref="DialoguePlayer.SaveState"/> captures
    /// the current position; you decide when to call it (e.g. on <c>OnLine</c> when the node
    /// has <c>IsCheckpoint = true</c>). On restore, enter-conditions and enter-actions of the
    /// checkpoint node re-fire, which is safe for idempotent checkpoint logic.
    /// </summary>
    [Serializable]
    public sealed class DialogueSessionState
    {
        /// <summary>GUID of the root DialogueGraph asset (via AssetDatabase).</summary>
        public string GraphGuid;

        /// <summary>ID of the node to resume from.</summary>
        public string NodeId;

        /// <summary>ISO 8601 timestamp of when the state was captured.</summary>
        public string SavedAt;

        /// <summary>Serialized context parameters: key → type|value pairs.</summary>
        public List<ContextEntry> ContextValues = new List<ContextEntry>();

        [Serializable]
        public sealed class ContextEntry
        {
            public string Key;
            public string Type;   // "bool" | "int" | "float" | "string"
            public string Value;  // invariant-culture string representation
        }

        // ── Capture ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Captures the current dialogue position and context into a new state object.
        /// </summary>
        public static DialogueSessionState Capture(string graphGuid, string nodeId, BaseContext context)
        {
            var state = new DialogueSessionState
            {
                GraphGuid = graphGuid,
                NodeId    = nodeId,
                SavedAt   = DateTime.UtcNow.ToString("o"),
            };

            if (context != null)
            {
                foreach (var kvp in context.GetAllVariables())
                {
                    var entry = SerializeValue(kvp.Key, kvp.Value);
                    if (entry != null) state.ContextValues.Add(entry);
                }
            }

            return state;
        }

        // ── Apply ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the saved context values back into <paramref name="context"/>.
        /// Keys already present are overwritten; unknown keys are added.
        /// </summary>
        public void ApplyContext(BaseContext context)
        {
            if (context == null) return;
            foreach (var entry in ContextValues)
                ApplyEntry(context, entry);
        }

        // ── JSON ──────────────────────────────────────────────────────────────────

        /// <summary>Serializes the state to a JSON string via Unity's JsonUtility.</summary>
        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: false);

        /// <summary>Deserializes a state from a JSON string produced by <see cref="ToJson"/>.</summary>
        public static DialogueSessionState FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<DialogueSessionState>(json); }
            catch (Exception ex)
            {
                Logging.Error("GraphDialogue", $"[GraphDialogue] DialogueSessionState.FromJson failed: {ex.Message}");
                return null;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ContextEntry SerializeValue(string key, object value)
        {
            if (value is bool b)
                return new ContextEntry { Key = key, Type = "bool", Value = b ? "true" : "false" };
            if (value is int i)
                return new ContextEntry { Key = key, Type = "int", Value = i.ToString(CultureInfo.InvariantCulture) };
            if (value is float f)
                return new ContextEntry { Key = key, Type = "float", Value = f.ToString("R", CultureInfo.InvariantCulture) };
            if (value is string s)
                return new ContextEntry { Key = key, Type = "string", Value = s };
            return null; // unsupported type — skip silently
        }

        private static void ApplyEntry(BaseContext context, ContextEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Key)) return;
            try
            {
                switch (entry.Type)
                {
                    case "bool":
                        context.Set(entry.Key, bool.Parse(entry.Value));
                        break;
                    case "int":
                        context.Set(entry.Key, int.Parse(entry.Value, CultureInfo.InvariantCulture));
                        break;
                    case "float":
                        context.Set(entry.Key, float.Parse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture));
                        break;
                    case "string":
                        context.Set(entry.Key, entry.Value);
                        break;
                    default:
                        Logging.Warning("GraphDialogue", $"[GraphDialogue] Unknown context type '{entry.Type}' for key '{entry.Key}' — skipped.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logging.Warning("GraphDialogue", $"[GraphDialogue] Could not restore context key '{entry.Key}': {ex.Message}");
            }
        }
    }
}
