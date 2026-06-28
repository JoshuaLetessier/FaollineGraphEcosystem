using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    public abstract partial class BaseGraphView
    {
        private static List<GraphTemplate> FindAllTemplates()
        {
            var result = new List<GraphTemplate>();
            var guids = AssetDatabase.FindAssets("t:GraphTemplate");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tpl = AssetDatabase.LoadAssetAtPath<GraphTemplate>(path);
                if (tpl != null) result.Add(tpl);
            }
            return result;
        }

        private void SaveSelectionAsTemplate()
        {
            var nodes = new List<BaseNodeData>();
            var edges = new List<BaseEdgeData>();

            foreach (var item in selection)
            {
                if (item is BaseNodeView nv && nv.NodeData != null)
                    nodes.Add(nv.NodeData);
                if (item is BaseEdgeView ev && ev.EdgeData != null)
                    edges.Add(ev.EdgeData);
            }

            if (nodes.Count == 0)
            {
                Debug.LogWarning("[GraphCore] No nodes selected — template not created.");
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject(
                "Save Graph Template", "NewTemplate", "asset",
                "Choose where to save the graph template.");
            if (string.IsNullOrEmpty(path)) return;

            var template = ScriptableObject.CreateInstance<GraphTemplate>();
            template.Capture(nodes, edges);
            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GraphCore] Template saved: {path} ({nodes.Count} nodes, {edges.Count} internal edges).");
        }

        private void InsertTemplate(GraphTemplate template, Vector2 insertPosition)
        {
            if (template == null || template.NodeCount == 0) return;

            var result = template.Instantiate(insertPosition);

            foreach (var json in result.NodeJsons)
            {
                var nodeData = DeserializeNode(json);
                if (nodeData == null) continue;

                if (result.IdMap.TryGetValue(nodeData.Id, out var newId))
                    nodeData.Id = newId;

                nodeData.Position += result.InsertPosition;

                var view = CreateNodeView(nodeData);
                if (view == null) continue;
                view.SetPosition(new Rect(nodeData.Position, Vector2.zero));
                AddElement(view);
                _nodeViews[nodeData.Id] = view;

                _graph?.AddNode(nodeData);
                _isDirty = true;
                OnNodeCreated(nodeData);
            }

            foreach (var json in result.EdgeJsons)
            {
                var edgeData = JsonUtility.FromJson<BaseEdgeData>(json);
                if (edgeData == null) continue;

                if (!result.IdMap.TryGetValue(edgeData.FromNodeId, out var newFrom)) continue;
                if (!result.IdMap.TryGetValue(edgeData.ToNodeId, out var newTo)) continue;

                edgeData.Id = System.Guid.NewGuid().ToString("D");
                edgeData.FromNodeId = newFrom;
                edgeData.ToNodeId = newTo;

                var edgeView = CreateEdgeViewForPaste(edgeData);
                if (edgeView == null) continue;

                _graph?.AddEdge(edgeData);
                ConnectEdgeView(edgeView, edgeData);
                AddElement(edgeView);
                _isDirty = true;
                OnEdgeConnected(edgeData);
            }
        }
    }
}
