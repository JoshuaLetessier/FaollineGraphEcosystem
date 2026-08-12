using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLogging;


namespace Faolline.GraphStandard.Editor
{
    /// <summary>
    /// Persists an in-memory graph (e.g. one built with <see cref="GraphBuilder{TGraph}"/>) as an asset, with
    /// its attached actions and conditions stored as SUB-ASSETS so the asset is self-contained and portable.
    /// Only objects that are not already persisted assets are added (a shared/asset condition is not
    /// double-added). The sweep is GENERIC: any <see cref="BaseCondition"/> / <see cref="BaseAction"/> field
    /// (single or in a <c>List&lt;&gt;</c>) on the graph itself or on any node subclass is collected — so a lib's
    /// own node type (e.g. an objective with a completion condition + reward) is handled without this builder
    /// knowing the type. Nested <see cref="BaseChoice.Condition"/> on choice nodes is also collected.
    /// </summary>
    public static class GraphAssetBuilder
    {
        /// <summary>Writes <paramref name="graph"/> to <paramref name="path"/> with its actions/conditions as
        /// sub-assets; returns the saved graph.</summary>
        public static BaseGraph Save(BaseGraph graph, string path)
        {
            if (graph == null)
            {
                Logging.Error("GraphStandard", "[GraphStandard] GraphAssetBuilder.Save: null graph; ignored.");
                return null;
            }
            if (string.IsNullOrEmpty(path))
            {
                Logging.Error("GraphStandard", "[GraphStandard] GraphAssetBuilder.Save: empty path; ignored.");
                return graph;
            }

            AssetDatabase.CreateAsset(graph, path);

            // Graph-level condition/action fields (e.g. a quest's unlock condition / completion reward).
            SweepReferencedScriptableObjects(graph, graph);

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                // Every BaseCondition/BaseAction field + list on the node (universal OnEnter/OnExit/Entry/Resume
                // AND any subclass-specific field) is collected generically.
                SweepReferencedScriptableObjects(graph, node);

                // BaseChoice.Condition lives one level down inside a List<BaseChoice> — collected explicitly.
                if (node is ChoiceNodeData choice)
                    foreach (var ch in choice.Choices)
                        if (ch != null) AddSubAsset(graph, ch.Condition);
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            return graph;
        }

        // Reflects over every field (public + private, up the inheritance chain) of <paramref name="holder"/> and
        // adds any BaseCondition/BaseAction value — or the BaseCondition/BaseAction items of a List<> field — as a
        // sub-asset of <paramref name="owner"/>.
        private static void SweepReferencedScriptableObjects(Object owner, object holder)
        {
            if (holder == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.DeclaredOnly;

            for (var type = holder.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(flags))
                {
                    var ft = field.FieldType;
                    if (typeof(BaseCondition).IsAssignableFrom(ft) || typeof(BaseAction).IsAssignableFrom(ft))
                    {
                        AddSubAsset(owner, field.GetValue(holder) as Object);
                    }
                    else if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var arg = ft.GetGenericArguments()[0];
                        if ((typeof(BaseCondition).IsAssignableFrom(arg) || typeof(BaseAction).IsAssignableFrom(arg))
                            && field.GetValue(holder) is IEnumerable items)
                            foreach (var item in items) AddSubAsset(owner, item as Object);
                    }
                }
            }
        }

        private static void AddSubAsset(Object owner, Object sub)
        {
            if (sub != null && !AssetDatabase.Contains(sub))
                AssetDatabase.AddObjectToAsset(sub, owner);
        }
    }
}
