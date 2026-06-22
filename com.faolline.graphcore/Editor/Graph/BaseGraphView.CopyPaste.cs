using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    public abstract partial class BaseGraphView
    {
        private static readonly Vector2 PasteOffset = new Vector2(30f, 30f);

        /// <summary>
        /// Serializes the selected graph elements to a JSON clipboard string.
        /// Only BaseNodeView and intra-selection BaseEdgeView elements are included.
        /// </summary>
        private string OnSerializeGraphElements(IEnumerable<GraphElement> elements)
        {
            var data = new GraphClipboardData();
            var selectedNodeIds = new HashSet<string>();

            foreach (var element in elements)
            {
                if (element is BaseNodeView nodeView && nodeView.NodeData != null)
                {
                    selectedNodeIds.Add(nodeView.NodeData.Id);
                    data.Nodes.Add(JsonUtility.ToJson(nodeView.NodeData));
                }
            }

            foreach (var element in elements)
            {
                if (element is BaseEdgeView edgeView && edgeView.EdgeData != null)
                {
                    var e = edgeView.EdgeData;
                    if (selectedNodeIds.Contains(e.FromNodeId) && selectedNodeIds.Contains(e.ToNodeId))
                        data.Edges.Add(JsonUtility.ToJson(edgeView.EdgeData));
                }
            }

            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// Deserializes clipboard data and pastes new nodes/edges with fresh GUIDs.
        /// All pasted nodes receive new GUIDs; edges are remapped to the new GUIDs.
        /// </summary>
        private void OnUnserializeAndPaste(string operationName, string data)
        {
            if (string.IsNullOrEmpty(data))
                return;

            GraphClipboardData clipboardData;
            try { clipboardData = JsonUtility.FromJson<GraphClipboardData>(data); }
            catch { return; }

            if (clipboardData == null)
                return;

            var oldToNew = new Dictionary<string, string>();
            var pastedNodes = new List<BaseNodeData>();

            foreach (var json in clipboardData.Nodes)
            {
                // We can't deserialize abstract BaseNodeData directly; the JSON carries the
                // concrete subtype. Use a type-dispatch approach via CreateNodeView factory.
                // For now, store the remapped ID mapping using a temporary parsed object.
                // Full deserialization is delegated to subclasses via a virtual hook.
                var placeholder = JsonUtility.FromJson<NodeIdPlaceholder>(json);
                if (placeholder == null || string.IsNullOrEmpty(placeholder.Id))
                    continue;

                var newId = Guid.NewGuid().ToString("D");
                oldToNew[placeholder.Id] = newId;
            }

            // Paste nodes via subclass factory — subclasses that support paste override
            // DeserializeNode to reconstruct concrete types.
            foreach (var json in clipboardData.Nodes)
            {
                var placeholder = JsonUtility.FromJson<NodeIdPlaceholder>(json);
                if (placeholder == null) continue;

                var nodeData = DeserializeNode(json);
                if (nodeData == null) continue;

                if (oldToNew.TryGetValue(nodeData.Id, out var newId))
                    nodeData.Id = newId;

                nodeData.Position += PasteOffset;
                pastedNodes.Add(nodeData);

                var view = CreateNodeView(nodeData);
                if (view == null) continue;
                view.SetPosition(new Rect(nodeData.Position, Vector2.zero));
                AddElement(view);
                _nodeViews[nodeData.Id] = view;

                _graph?.AddNode(nodeData);
                _isDirty = true;
                OnNodeCreated(nodeData);
            }

            foreach (var json in clipboardData.Edges)
            {
                var edgeData = JsonUtility.FromJson<BaseEdgeData>(json);
                if (edgeData == null) continue;

                if (!oldToNew.TryGetValue(edgeData.FromNodeId, out var newFrom)) continue;
                if (!oldToNew.TryGetValue(edgeData.ToNodeId, out var newTo)) continue;

                edgeData.Id = Guid.NewGuid().ToString("D");
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

        /// <summary>
        /// Deserializes a JSON string into a concrete BaseNodeData instance via
        /// <see cref="NodeTypeDeserializationRegistry"/>. Override to add custom deserialization
        /// for types not registered in the registry.
        /// </summary>
        protected virtual BaseNodeData DeserializeNode(string json)
            => NodeTypeDeserializationRegistry.Deserialize(json);

        /// <summary>
        /// Override to create a BaseEdgeView for a pasted edge without connecting ports.
        /// Base implementation delegates to CreateEdgeView.
        /// </summary>
        protected virtual BaseEdgeView CreateEdgeViewForPaste(BaseEdgeData edgeData) => CreateEdgeView(edgeData);

        [Serializable]
        private class NodeIdPlaceholder
        {
            // Must match the [SerializeField] backing field name in BaseNodeData, not the property name.
            public string _id;
            public string Id => _id;
        }
    }
}
