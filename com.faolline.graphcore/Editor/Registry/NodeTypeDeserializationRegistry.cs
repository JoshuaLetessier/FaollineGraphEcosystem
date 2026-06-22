using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Maps <see cref="BaseNodeData.NodeType"/> strings to their concrete <see cref="System.Type"/>,
    /// so editor operations (paste, template insert) can reconstruct the right subclass from JSON.
    /// Auto-discovers all non-abstract <see cref="BaseNodeData"/> subclasses that declare a
    /// <c>public const string NodeTypeId</c> field via <see cref="TypeCache"/> at editor load.
    /// </summary>
    public static class NodeTypeDeserializationRegistry
    {
        private static readonly Dictionary<string, Type> _map = new Dictionary<string, Type>();

        /// <summary>Maps <paramref name="nodeTypeId"/> to <paramref name="type"/>. Overwrites silently.</summary>
        public static void Register(string nodeTypeId, Type type)
        {
            if (string.IsNullOrEmpty(nodeTypeId) || type == null) return;
            _map[nodeTypeId] = type;
        }

        /// <summary>Deserializes <paramref name="json"/> into the concrete <see cref="BaseNodeData"/>
        /// subclass matching its <c>NodeType</c> field. Returns null if the type is unknown.</summary>
        public static BaseNodeData Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var probe = JsonUtility.FromJson<NodeTypeProbe>(json);
            if (probe == null || string.IsNullOrEmpty(probe.NodeType)) return null;
            if (!_map.TryGetValue(probe.NodeType, out var type)) return null;
            return JsonUtility.FromJson(json, type) as BaseNodeData;
        }

        [InitializeOnLoadMethod]
        private static void AutoDiscover()
        {
            _map.Clear();
            var types = TypeCache.GetTypesDerivedFrom<BaseNodeData>();
            foreach (var type in types)
            {
                if (type.IsAbstract) continue;
                var field = type.GetField("NodeTypeId",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (field == null || !field.IsLiteral || field.FieldType != typeof(string)) continue;
                var id = (string)field.GetRawConstantValue();
                if (!string.IsNullOrEmpty(id))
                    _map[id] = type;
            }
        }

        [Serializable]
        private class NodeTypeProbe { public string NodeType; }
    }
}
