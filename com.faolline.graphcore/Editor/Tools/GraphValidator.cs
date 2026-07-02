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
                if (nodes.Count > 1 && !connected.Contains(n.Id))
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

            return report;
        }

        // Signal names the graph AWAITS but never RAISES within itself — so they must arrive from outside.
        // Only inspects the given graph's own nodes (one level; nested sub-graphs are validated separately).
        private static IEnumerable<string> ExternalAwaitedSignals(BaseGraph graph)
        {
            var awaited = new HashSet<string>();
            var raised = new HashSet<string>();
            if (graph?.Nodes == null) return awaited;

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                if (!string.IsNullOrEmpty(node.AwaitSignalName)) awaited.Add(node.AwaitSignalName);
                CollectRaised(node.OnEnterActions, raised);
                CollectRaised(node.OnExitActions, raised);
            }
            awaited.ExceptWith(raised);
            return awaited;
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
