using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Injects a "Category Groups" section into the graph inspector (no-selection panel) via
    /// <see cref="InspectorExtensionRegistry"/>. Reference worked example for the registry: see
    /// EXTENSIBILITY.md at the repo root. Auto-registered at editor load.
    /// <para>
    /// Unlike a section that only edits data embedded on the inspected graph (e.g.
    /// <c>LocalizationInspectorExtension</c>), this one mutates a <em>foreign</em> asset — the
    /// <see cref="GraphCategoryGroup"/>(s) the graph belongs to — so it dirties/saves that asset itself
    /// rather than relying on the <c>markDirty</c> callback, which only covers the graph being inspected.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class GraphCategoryGroupInspectorExtension
    {
        static GraphCategoryGroupInspectorExtension()
        {
            InspectorExtensionRegistry.RegisterGraphSection(BuildGraphSection);
        }

        private static void BuildGraphSection(BaseGraph graph, VisualElement parent, Action markDirty)
        {
            var foldout = new Foldout { text = "Category Groups", value = false };
            foldout.style.marginTop = 4;
            parent.Add(foldout);

            Rebuild(foldout, graph);
        }

        private static void Rebuild(Foldout foldout, BaseGraph graph)
        {
            foldout.Clear();

            foreach (var group in FindGroupsContaining(graph))
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
                row.Add(new Label(group.Label) { style = { flexGrow = 1 } });
                row.Add(new Button(() =>
                {
                    Undo.RecordObject(group, "Remove Graph From Category Group");
                    group.Remove(graph);
                    EditorUtility.SetDirty(group);
                    Rebuild(foldout, graph);
                })
                { text = "Remove" });
                foldout.Add(row);
            }

            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            var picker = new ObjectField { objectType = typeof(GraphCategoryGroup), allowSceneObjects = false, style = { flexGrow = 1 } };
            addRow.Add(picker);
            addRow.Add(new Button(() =>
            {
                if (picker.value is GraphCategoryGroup group && !group.Contains(graph))
                {
                    Undo.RecordObject(group, "Add Graph To Category Group");
                    group.Add(graph);
                    EditorUtility.SetDirty(group);
                    picker.value = null;
                    Rebuild(foldout, graph);
                }
            })
            { text = "Add To Group" });
            foldout.Add(addRow);
        }

        /// <summary>Reverse-scans project <see cref="GraphCategoryGroup"/> assets for membership — group → graph
        /// is the only stored direction. Runs once per inspector bind (selection change), not per redraw.</summary>
        private static IEnumerable<GraphCategoryGroup> FindGroupsContaining(BaseGraph graph)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(GraphCategoryGroup)))
            {
                var group = AssetDatabase.LoadAssetAtPath<GraphCategoryGroup>(AssetDatabase.GUIDToAssetPath(guid));
                if (group != null && group.Contains(graph))
                    yield return group;
            }
        }
    }
}
