using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLogging;


namespace Faolline.GraphSave
{
    /// <summary>
    /// A neutral, serializable snapshot of a running graph: the execution context's parameters and collections
    /// plus the current node id (and the graph id it belongs to). This is the whole save model — capture it with
    /// <see cref="Capture(BaseContext, string, string)"/>, persist it however you like (it is a plain
    /// JsonUtility-serializable object), and put a run back with <see cref="ApplyTo"/> + <c>BaseRunner.StartFrom</c>
    /// (or the <see cref="Restore"/> convenience). graphcore exposes everything needed, so this layer adds no
    /// engine dependency and is fully testable headlessly.
    /// </summary>
    [Serializable]
    public class GraphRunSnapshot
    {
        /// <summary>The <see cref="BaseGraph.GraphId"/> this snapshot was taken from (informational).</summary>
        public string GraphId;

        /// <summary>The id of the node the run was on when captured; restore re-enters here.</summary>
        public string CurrentNodeId;

        /// <summary>The context's typed parameters, flattened for serialization.</summary>
        public List<Param> Variables = new List<Param>();

        /// <summary>The context's named string collections, with their item quantities.</summary>
        public List<Collection> Collections = new List<Collection>();

        /// <summary>Every signal name that had been raised at least once in the context at capture time.</summary>
        public List<string> RaisedSignals = new List<string>();

        /// <summary>One context parameter, value flattened to a string with a type tag.</summary>
        [Serializable]
        public class Param
        {
            public string Key;
            public string Type;   // "bool" | "int" | "float" | "string" | "vector2" | "vector3" | "color"
            public string Value;  // every value is flattened to an invariant string (vectors/color: comma-separated components)
        }

        /// <summary>
        /// One named string collection from the context: distinct items in insertion order, plus their
        /// quantities (graphcore 0.32.0 stacking). <see cref="Counts"/> is a NEW, additive field — a
        /// snapshot captured before it existed deserializes it as an empty list; <see cref="ApplyTo"/>
        /// treats a missing/absent count as quantity 1, exactly the pre-stacking behavior. Old save files
        /// keep loading unchanged.
        /// </summary>
        [Serializable]
        public class Collection
        {
            public string Key;
            public List<string> Items = new List<string>();

            /// <summary>
            /// Parallel to <see cref="Items"/> (same index = same item). Absent, short, or a non-positive
            /// entry all mean "quantity 1" — the pre-0.6.0 behavior for that item.
            /// </summary>
            public List<int> Counts = new List<int>();
        }

        /// <summary>Captures <paramref name="context"/>'s parameters + collections (with quantities), tagged with the graph/node ids.</summary>
        public static GraphRunSnapshot Capture(BaseContext context, string graphId = null, string currentNodeId = null)
        {
            var snapshot = new GraphRunSnapshot { GraphId = graphId, CurrentNodeId = currentNodeId };
            if (context != null)
            {
                foreach (var kv in context.GetAllVariables())
                    snapshot.Variables.Add(ToParam(kv.Key, kv.Value));

                // GetAllCollections() gives the key set for free; the actual (item, quantity) pairs come
                // from GetCollectionWithCounts (graphcore 0.32.0) so a stacked item's quantity round-trips.
                foreach (var key in context.GetAllCollections().Keys)
                {
                    var collection = new Collection { Key = key };
                    foreach (var (item, count) in context.GetCollectionWithCounts(key))
                    {
                        collection.Items.Add(item);
                        collection.Counts.Add(count);
                    }
                    snapshot.Collections.Add(collection);
                }

                snapshot.RaisedSignals.AddRange(context.GetAllRaisedSignals());
            }
            return snapshot;
        }

        /// <summary>
        /// Convenience capture reading the live cursor (graph id + current node) off a runner. Note this records
        /// the TOP frame's node only — capture at TOP-LEVEL checkpoints, not while the run has descended into a
        /// sub-graph (see <see cref="Restore"/> for why a mid-sub-graph node cannot be restored).
        /// </summary>
        public static GraphRunSnapshot Capture(BaseRunner runner, BaseContext context)
            => Capture(context, runner?.CurrentGraph?.GraphId, runner?.CurrentNode?.Id);

        /// <summary>
        /// Writes this snapshot's parameters and collections back into <paramref name="context"/>. Variables
        /// overwrite (a <c>Set</c>); collections, by default, are MERGED (items are added) — so applying onto an
        /// already-populated context can double entries. Pass <paramref name="replaceCollections"/> = <c>true</c>
        /// to clear each captured collection key first, making the snapshot authoritative (what <see cref="Restore"/>
        /// does). Default <c>false</c> keeps the additive behavior.
        /// <para>
        /// <b>Quantities and merge mode:</b> an item captured at quantity &gt; 1 is applied via the additive
        /// stacking overload, which is never idempotent (graphcore 0.32.0). With <paramref name="replaceCollections"/>
        /// = <c>false</c>, calling <see cref="ApplyTo"/> more than once with the SAME snapshot stacks that
        /// quantity again each time. This is a non-issue with <c>true</c> (the default <see cref="Restore"/>
        /// path) since each call starts from an empty collection; a merge-mode consumer that needs idempotent
        /// re-apply should clear the affected collections itself first.
        /// </para>
        /// </summary>
        public void ApplyTo(BaseContext context, bool replaceCollections = false)
        {
            if (context == null) return;

            foreach (var p in Variables) ApplyParam(context, p);

            foreach (var c in Collections)
            {
                if (c == null || c.Items == null) continue;
                if (replaceCollections) context.ClearCollection(c.Key);
                for (int i = 0; i < c.Items.Count; i++)
                {
                    var item = c.Items[i];
                    var count = (c.Counts != null && i < c.Counts.Count) ? c.Counts[i] : 1;
                    if (count > 1) context.AddToCollection(c.Key, item, count);
                    else context.AddToCollection(c.Key, item);
                }
            }

            if (RaisedSignals != null && RaisedSignals.Count > 0)
                context.RestoreSignalHistory(RaisedSignals);
        }

        /// <summary>
        /// Restores a run: applies this snapshot to <paramref name="context"/> (collections replaced, so the
        /// snapshot is authoritative), then re-enters <paramref name="runner"/> at the saved node (or the graph
        /// entry when none) via <c>StartFrom</c>.
        /// <para>
        /// <b>Top-level only.</b> <see cref="CurrentNodeId"/> is the TOP frame's node, and the snapshot does NOT
        /// capture the execution stack. So restoring works for a node in <paramref name="graph"/> itself; a node
        /// saved while the run had descended into a SUB-GRAPH (e.g. mid-dialogue) is not in <paramref name="graph"/>
        /// and cannot be re-entered this way. Capture/restore at TOP-LEVEL checkpoints — pair this with
        /// <c>BaseNodeData.IsCheckpoint</c> nodes (a checkpoint placed just before a long, non-replayable sequence
        /// doubles as the save point: on load the run re-enters the checkpoint and the sequence simply replays).
        /// </para>
        /// </summary>
        public void Restore(BaseRunner runner, BaseGraph graph, BaseContext context, NodeExecutorRegistry registry = null)
        {
            if (runner == null || graph == null || context == null) return;
            ApplyTo(context, replaceCollections: true);
            var nodeId = string.IsNullOrEmpty(CurrentNodeId) ? graph.EntryNodeId : CurrentNodeId;
            runner.StartFrom(graph, nodeId, context, registry ?? new NodeExecutorRegistry());
        }

        // ── Type-tagged (de)serialization of context parameters ─────────────────────
        private static Param ToParam(string key, object value)
        {
            switch (value)
            {
                case bool b:    return new Param { Key = key, Type = "bool",    Value = b ? "1" : "0" };
                case int i:     return new Param { Key = key, Type = "int",     Value = i.ToString(CultureInfo.InvariantCulture) };
                case float f:   return new Param { Key = key, Type = "float",   Value = f.ToString(CultureInfo.InvariantCulture) };
                case string s:  return new Param { Key = key, Type = "string",  Value = s };
                case Vector2 v2: return new Param { Key = key, Type = "vector2", Value = Join(v2.x, v2.y) };
                case Vector3 v3: return new Param { Key = key, Type = "vector3", Value = Join(v3.x, v3.y, v3.z) };
                case Color c:    return new Param { Key = key, Type = "color",   Value = Join(c.r, c.g, c.b, c.a) };
                default:        return new Param { Key = key, Type = "string",  Value = value?.ToString() ?? string.Empty };
            }
        }

        /// <remarks>
        /// On a malformed value (corrupted/edited/outdated save), the key is skipped with a
        /// <c>[GraphSave]</c> warning rather than crashing the restore. An unknown type tag
        /// falls back to string.
        /// </remarks>
        private static void ApplyParam(BaseContext context, Param p)
        {
            if (p == null || string.IsNullOrEmpty(p.Key)) return;
            switch (p.Type)
            {
                case "bool":
                    context.Set<bool>(p.Key, p.Value == "1" || string.Equals(p.Value, "true", StringComparison.OrdinalIgnoreCase));
                    break;
                case "int":
                    if (int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        context.Set<int>(p.Key, i);
                    else
                        Logging.Warning("GraphSave", $"[GraphSave] Skipping param '{p.Key}': cannot parse '{p.Value}' as int.");
                    break;
                case "float":
                    if (float.TryParse(p.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        context.Set<float>(p.Key, f);
                    else
                        Logging.Warning("GraphSave", $"[GraphSave] Skipping param '{p.Key}': cannot parse '{p.Value}' as float.");
                    break;
                case "vector2":
                    if (TryParseComponents(p.Value, 2, out var v2))
                        context.Set<Vector2>(p.Key, new Vector2(v2[0], v2[1]));
                    else
                        Logging.Warning("GraphSave", $"[GraphSave] Skipping param '{p.Key}': cannot parse '{p.Value}' as Vector2.");
                    break;
                case "vector3":
                    if (TryParseComponents(p.Value, 3, out var v3))
                        context.Set<Vector3>(p.Key, new Vector3(v3[0], v3[1], v3[2]));
                    else
                        Logging.Warning("GraphSave", $"[GraphSave] Skipping param '{p.Key}': cannot parse '{p.Value}' as Vector3.");
                    break;
                case "color":
                    if (TryParseComponents(p.Value, 4, out var c))
                        context.Set<Color>(p.Key, new Color(c[0], c[1], c[2], c[3]));
                    else
                        Logging.Warning("GraphSave", $"[GraphSave] Skipping param '{p.Key}': cannot parse '{p.Value}' as Color.");
                    break;
                default:
                    context.Set<string>(p.Key, p.Value ?? string.Empty);
                    break;
            }
        }

        // Value-type params are flattened to a comma-separated, invariant-culture component string so the snapshot
        // stays a plain POCO that round-trips through JsonUtility AND any reflection-based JSON backend (no raw Unity
        // structs to choke on).
        private static string Join(params float[] components)
            => string.Join(",", System.Array.ConvertAll(components, f => f.ToString(CultureInfo.InvariantCulture)));

        private static bool TryParseComponents(string value, int expected, out float[] components)
        {
            components = new float[expected];
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split(',');
            if (parts.Length != expected) return false;
            for (int i = 0; i < expected; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out components[i]))
                    return false;
            return true;
        }
    }
}
