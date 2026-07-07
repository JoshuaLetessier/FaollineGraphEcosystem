using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>Severity of a <see cref="GraphIssue"/>.</summary>
    public enum GraphIssueSeverity { Info, Warning, Error }

    /// <summary>A single validation finding. <see cref="NodeId"/> is null/empty for graph-level issues.</summary>
    public readonly struct GraphIssue
    {
        public readonly GraphIssueSeverity Severity;
        public readonly string NodeId;
        public readonly string Message;

        public GraphIssue(GraphIssueSeverity severity, string nodeId, string message)
        {
            Severity = severity;
            NodeId = nodeId;
            Message = message;
        }
    }

    /// <summary>Result of validating a graph: a flat list of issues with quick counts.</summary>
    public sealed class GraphValidationReport
    {
        public readonly List<GraphIssue> Issues = new List<GraphIssue>();

        public int ErrorCount => Issues.Count(i => i.Severity == GraphIssueSeverity.Error);
        public int WarningCount => Issues.Count(i => i.Severity == GraphIssueSeverity.Warning);
        public bool HasErrors => ErrorCount > 0;
    }

    /// <summary>
    /// Static structural validation for any <see cref="BaseGraph"/> (reusable across libs). Reports the
    /// authoring mistakes that the runtime cannot recover from gracefully: missing/duplicate Start,
    /// missing End, edges to/from non-existent nodes, isolated nodes, choices without options, and choice
    /// options with no outgoing edge. Pure logic (no scene/Unity state) so it is fully EditMode-testable.
    /// Menu: <c>Faolline ▸ Graph ▸ Validate Selected Graph</c>.
    /// </summary>
    public static class GraphValidator
    {
        public static GraphValidationReport Validate(BaseGraph graph)
        {
            var report = new GraphValidationReport();
            if (graph == null)
            {
                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null, "Graph is null."));
                return report;
            }

            var nodes = graph.Nodes?.Where(n => n != null).ToList() ?? new List<BaseNodeData>();
            var edges = graph.Edges?.Where(e => e != null).ToList() ?? new List<BaseEdgeData>();
            var ids = new HashSet<string>(nodes.Select(n => n.Id));

            // ── Graph-level ───────────────────────────────────────────────────
            var startCount = nodes.Count(n => n.NodeType == StartNodeData.NodeTypeId);
            if (startCount == 0)
                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null, "No Start node — the graph has no entry point."));
            else if (startCount > 1)
                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null, $"{startCount} Start nodes — exactly one is required."));

            if (string.IsNullOrEmpty(graph.EntryNodeId))
                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Warning, null, "EntryNodeId is not set."));
            else if (!ids.Contains(graph.EntryNodeId))
                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null, $"EntryNodeId '{graph.EntryNodeId}' matches no node."));

            if (!nodes.Any(n => n.NodeType == EndNodeData.NodeTypeId))
                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Warning, null,
                    "No End node — the graph can only terminate by running out of edges."));

            foreach (var e in edges)
            {
                if (!string.IsNullOrEmpty(e.FromNodeId) && !ids.Contains(e.FromNodeId))
                    report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null, $"Edge from a non-existent node '{e.FromNodeId}'."));
                if (!string.IsNullOrEmpty(e.ToNodeId) && !ids.Contains(e.ToNodeId))
                    report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null, $"Edge to a non-existent node '{e.ToNodeId}'."));
            }

            // ── Node-level ────────────────────────────────────────────────────
            var connected = new HashSet<string>();
            foreach (var e in edges)
            {
                if (!string.IsNullOrEmpty(e.FromNodeId)) connected.Add(e.FromNodeId);
                if (!string.IsNullOrEmpty(e.ToNodeId)) connected.Add(e.ToNodeId);
            }

            foreach (var n in nodes)
            {
                // GraphLink is a non-executing documentary annotation — being unconnected is its normal state,
                // so exempt it from the isolated-node warning (a false positive there would teach consumers to
                // ignore validator warnings, killing its value).
                if (nodes.Count > 1 && !connected.Contains(n.Id) && !(n is GraphLinkNodeData))
                    report.Issues.Add(new GraphIssue(GraphIssueSeverity.Warning, n.Id, $"Isolated node '{Label(n)}' (no connection)."));

                if (n is ChoiceNodeData choice)
                {
                    var options = choice.Choices?.Where(c => c != null).ToList() ?? new List<BaseChoice>();
                    if (options.Count == 0)
                    {
                        report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, n.Id, $"Choice node '{Label(n)}' has no options."));
                    }
                    else
                    {
                        foreach (var opt in options)
                        {
                            bool hasEdge = edges.Any(e => e.FromNodeId == n.Id && e.PortName == opt.Id);
                            if (!hasEdge)
                                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, n.Id,
                                    $"Choice option '{(string.IsNullOrEmpty(opt.Title) ? opt.Id : opt.Title)}' has no outgoing edge."));
                        }
                    }
                }

                // Auto-advanced node (NOT a choice — choices route by ChooseById/port, order-independent): an
                // unconditioned outgoing edge that is not LAST shadows every edge after it, because the runner
                // takes the first edge whose condition passes and an unconditioned edge always passes. The later
                // branches are then dead. (An unconditioned edge placed LAST is the valid default/else branch.)
                if (!(n is ChoiceNodeData))
                {
                    var outs = edges.Where(e => e.FromNodeId == n.Id).ToList();
                    if (outs.Count > 1)
                    {
                        int firstOpen = outs.FindIndex(e => e.Condition == null);
                        if (firstOpen >= 0 && firstOpen < outs.Count - 1)
                            report.Issues.Add(new GraphIssue(GraphIssueSeverity.Warning, n.Id,
                                $"Node '{Label(n)}' has an unconditioned outgoing edge that is not last, so the " +
                                $"{outs.Count - 1 - firstOpen} branch(es) after it are unreachable (the runner takes " +
                                $"the first passing edge). Add a condition to it, or move it last as the default branch."));
                    }
                }

                // Sub-graph on a FRESH context (Inherit off, no scope) that awaits a signal never raised inside
                // itself: the signal can only come from the parent/host, which writes a DIFFERENT context, so the
                // await can never resume — a guaranteed deadlock. (A self-contained subgraph that raises what it
                // awaits is fine on a fresh context, so we only flag the truly-external awaits.)
                if (n is SubGraphNodeData sub && sub.TargetGraph != null
                    && !sub.InheritParentContext && !sub.OpensScope)
                {
                    foreach (var signal in ExternalAwaitedSignals(sub.TargetGraph))
                        report.Issues.Add(new GraphIssue(GraphIssueSeverity.Warning, n.Id,
                            $"Sub-graph '{Label(n)}' runs on a fresh context (Inherit Parent Context off, no " +
                            $"scope) but its target awaits signal '{signal}' that nothing inside it raises — the " +
                            $"signal from the parent/host can never cross into the fresh context, so it deadlocks. " +
                            $"Enable Inherit Parent Context (or Opens Scope) if that signal must reach it."));
                }
            }

            CheckCircularAwaits(graph, nodes, edges, report);
            CheckParameterTypeMismatches(graph, report);

            return report;
        }

        // ── Variable type-safety ────────────────────────────────────────────
        // A VariableDef asset carries the authoritative type. An action/condition tags each reference with the
        // type it needs (via IVariableReferencing / VariableReference). A mismatch (e.g. a SetIntAction wired to
        // a Float parameter, or two differently-typed actions sharing one parameter) silently corrupts the key at
        // runtime under the old raw-string model — here it is an authoring-time error. Reported once per distinct
        // (parameter, wrong-expected-type) pair to avoid duplicate spam when a parameter is referenced many times.
        private static void CheckParameterTypeMismatches(BaseGraph graph, GraphValidationReport report)
        {
            var reported = new HashSet<string>();
            foreach (var reference in GraphVariableScanner.CollectReferences(graph))
            {
                var param = reference.Variable;
                if (param == null || param.Type == reference.ExpectedType) continue;

                var dedupKey = $"{param.Key}|{reference.ExpectedType}";
                if (!reported.Add(dedupKey)) continue;

                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null,
                    $"Variable '{param.DisplayName}' is typed {param.Type} but is referenced as " +
                    $"{reference.ExpectedType} by an action/condition. Fix the parameter's type or use a " +
                    $"{reference.ExpectedType} parameter there — the types must match."));
            }
        }

        // ── Circular await ───────────────────────────────────────────────────
        // An awaiting node parks BEFORE its exit-actions run and before anything downstream executes. So an
        // internal raiser of the awaited signal only helps if it can run WITHOUT the awaiting node resuming:
        // it must be reachable from the entry with the awaiting node treated as absorbing (can be entered,
        // never left). If the signal IS raised in this graph but every raiser sits on the awaiting node's own
        // exit-actions or strictly behind it, the await can never resume from inside the graph — the
        // "cupboard" deadlock (a flow that awaits a signal only its own completion raises). A name never
        // raised internally stays exempt: it is presumed to come from the host (the normal await pattern;
        // the fresh-context sub-graph lint above covers the isolated case).
        private static void CheckCircularAwaits(BaseGraph graph, List<BaseNodeData> nodes,
            List<BaseEdgeData> edges, GraphValidationReport report)
        {
            if (string.IsNullOrEmpty(graph.EntryNodeId)) return;   // no entry → no reachability to reason about

            var raisedAnywhere = new HashSet<string>();
            foreach (var n in nodes)
            {
                CollectRaised(n.OnEnterActions, raisedAnywhere);
                CollectRaised(n.OnExitActions, raisedAnywhere);
            }
            if (raisedAnywhere.Count == 0) return;

            var byId = new Dictionary<string, BaseNodeData>();
            foreach (var n in nodes)
                if (!string.IsNullOrEmpty(n.Id) && !byId.ContainsKey(n.Id)) byId[n.Id] = n;

            var adjacency = new Dictionary<string, List<string>>();
            foreach (var e in edges)
            {
                if (string.IsNullOrEmpty(e.FromNodeId) || string.IsNullOrEmpty(e.ToNodeId)) continue;
                if (!adjacency.TryGetValue(e.FromNodeId, out var list)) adjacency[e.FromNodeId] = list = new List<string>();
                list.Add(e.ToNodeId);
            }

            foreach (var awaiting in nodes)
            {
                var names = awaiting.AwaitSignalNames;
                if (names.Count == 0) continue;
                // Any awaited name never raised internally → presumed host-raised → resumable; skip.
                if (names.Any(x => !raisedAnywhere.Contains(x))) continue;

                var beforeResume = RaisedBeforeResume(graph, byId, adjacency, awaiting);
                // OR-await: resumable if ANY awaited name can be raised before the node resumes.
                if (names.Any(x => beforeResume.Contains(x))) continue;

                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Warning, awaiting.Id,
                    $"Circular await on node '{Label(awaiting)}': it awaits " +
                    $"'{string.Join("' / '", names)}', and every raiser of {(names.Count > 1 ? "those signals" : "that signal")} " +
                    $"in this graph runs only AFTER this node resumes (its own exit-actions, or nodes behind it) — " +
                    $"the await can never resume from inside the graph. Raise the signal before/parallel to this " +
                    $"node, or from the host."));
            }
        }

        // Every signal raisable from the entry while <paramref name="awaiting"/> is parked: enter-actions of
        // all reachable nodes (the awaiting node's own enter-actions run before it parks), exit-actions of all
        // reachable nodes EXCEPT the awaiting one, with the awaiting node absorbing traversal.
        private static HashSet<string> RaisedBeforeResume(BaseGraph graph,
            Dictionary<string, BaseNodeData> byId, Dictionary<string, List<string>> adjacency, BaseNodeData awaiting)
        {
            var raised = new HashSet<string>();
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(graph.EntryNodeId);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!visited.Add(id) || !byId.TryGetValue(id, out var node)) continue;

                CollectRaised(node.OnEnterActions, raised);
                if (ReferenceEquals(node, awaiting)) continue;   // absorbing: no exit-actions, no traversal out
                CollectRaised(node.OnExitActions, raised);

                if (adjacency.TryGetValue(id, out var next))
                    foreach (var to in next) queue.Enqueue(to);
            }
            return raised;
        }

        // Signal names the graph AWAITS but never RAISES within itself — so they must arrive from outside.
        // Only inspects the given graph's own nodes (one level; nested sub-graphs are validated separately).
        // Multi-await is a logical OR: a node deadlocks only when NONE of its awaited names is raised inside
        // the graph, so a node awaiting {internal, external} is fine and reports nothing.
        private static IEnumerable<string> ExternalAwaitedSignals(BaseGraph graph)
        {
            var result = new List<string>();
            if (graph?.Nodes == null) return result;

            var raised = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                CollectRaised(node.OnEnterActions, raised);
                CollectRaised(node.OnExitActions, raised);
            }

            var seen = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                var names = node.AwaitSignalNames;
                if (names.Count == 0) continue;
                bool anyInternal = false;
                foreach (var n in names)
                    if (raised.Contains(n)) { anyInternal = true; break; }
                if (anyInternal) continue;
                foreach (var n in names)
                    if (seen.Add(n)) result.Add(n);
            }
            return result;
        }

        private static void CollectRaised(List<BaseAction> actions, HashSet<string> into)
        {
            if (actions == null) return;
            foreach (var a in actions)
                if (a is RaiseSignalAction rs && rs.Signal != null)
                {
                    string name = (string)rs.Signal;
                    if (!string.IsNullOrEmpty(name)) into.Add(name);
                }
        }

        private static string Label(BaseNodeData n) => string.IsNullOrEmpty(n.Title) ? n.Id : n.Title;

        // ── Menu + logging ──────────────────────────────────────────────────

        [MenuItem("Faolline/Graph/Validate Selected Graph")]
        private static void ValidateSelected()
        {
            if (!(Selection.activeObject is BaseGraph graph))
            {
                Debug.LogWarning("[GraphValidator] Select a graph asset (BaseGraph or a subclass) first.");
                return;
            }
            LogReport(graph.name, Validate(graph));
        }

        /// <summary>Logs a report to the console (errors as LogError, warnings as LogWarning).</summary>
        public static void LogReport(string title, GraphValidationReport report)
        {
            if (report == null) return;
            Debug.Log($"[GraphValidator] '{title}' — {report.ErrorCount} error(s), {report.WarningCount} warning(s).");
            foreach (var issue in report.Issues)
            {
                var where = string.IsNullOrEmpty(issue.NodeId) ? "[Graph]" : $"[Node {issue.NodeId}]";
                var msg = $"[GraphValidator] {where} {issue.Message}";
                switch (issue.Severity)
                {
                    case GraphIssueSeverity.Error: Debug.LogError(msg); break;
                    case GraphIssueSeverity.Warning: Debug.LogWarning(msg); break;
                    default: Debug.Log(msg); break;
                }
            }
        }
    }
}
