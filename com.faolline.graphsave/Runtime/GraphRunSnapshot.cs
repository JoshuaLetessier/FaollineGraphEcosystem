using System;
using System.Collections.Generic;
using System.Globalization;
using Faolline.GraphCore;

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
        public List<Param> Parameters = new List<Param>();

        /// <summary>The context's named string collections.</summary>
        public List<Collection> Collections = new List<Collection>();

        /// <summary>One context parameter, value flattened to a string with a type tag.</summary>
        [Serializable]
        public class Param
        {
            public string Key;
            public string Type;   // "bool" | "int" | "float" | "string"
            public string Value;
        }

        /// <summary>One named string collection from the context.</summary>
        [Serializable]
        public class Collection
        {
            public string Key;
            public List<string> Items = new List<string>();
        }

        /// <summary>Captures <paramref name="context"/>'s parameters + collections, tagged with the graph/node ids.</summary>
        public static GraphRunSnapshot Capture(BaseContext context, string graphId = null, string currentNodeId = null)
        {
            var snapshot = new GraphRunSnapshot { GraphId = graphId, CurrentNodeId = currentNodeId };
            if (context != null)
            {
                foreach (var kv in context.GetAllParameters())
                    snapshot.Parameters.Add(ToParam(kv.Key, kv.Value));

                foreach (var kv in context.GetAllCollections())
                {
                    var collection = new Collection { Key = kv.Key };
                    if (kv.Value != null) collection.Items.AddRange(kv.Value);
                    snapshot.Collections.Add(collection);
                }
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
        /// Writes this snapshot's parameters and collections back into <paramref name="context"/>. Parameters
        /// overwrite (a <c>Set</c>); collections, by default, are MERGED (items are added) — so applying onto an
        /// already-populated context can double entries. Pass <paramref name="replaceCollections"/> = <c>true</c>
        /// to clear each captured collection key first, making the snapshot authoritative (what <see cref="Restore"/>
        /// does). Default <c>false</c> keeps the additive behavior.
        /// </summary>
        public void ApplyTo(BaseContext context, bool replaceCollections = false)
        {
            if (context == null) return;

            foreach (var p in Parameters) ApplyParam(context, p);

            foreach (var c in Collections)
            {
                if (c == null) continue;
                if (replaceCollections) context.ClearCollection(c.Key);
                if (c.Items != null)
                    foreach (var item in c.Items)
                        context.AddToCollection(c.Key, item);
            }
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
                case bool b:   return new Param { Key = key, Type = "bool",   Value = b ? "1" : "0" };
                case int i:    return new Param { Key = key, Type = "int",    Value = i.ToString(CultureInfo.InvariantCulture) };
                case float f:  return new Param { Key = key, Type = "float",  Value = f.ToString(CultureInfo.InvariantCulture) };
                case string s: return new Param { Key = key, Type = "string", Value = s };
                default:       return new Param { Key = key, Type = "string", Value = value?.ToString() ?? string.Empty };
            }
        }

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
                    break;
                case "float":
                    if (float.TryParse(p.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        context.Set<float>(p.Key, f);
                    break;
                default:
                    context.Set<string>(p.Key, p.Value ?? string.Empty);
                    break;
            }
        }
    }
}
