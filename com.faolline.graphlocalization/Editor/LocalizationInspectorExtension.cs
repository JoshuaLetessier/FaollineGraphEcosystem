using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Injects localization UI (per-node asset flags + graph-level defaults) into the graph
    /// inspector via <see cref="InspectorExtensionRegistry"/>. Auto-registered at editor load.
    /// </summary>
    [InitializeOnLoad]
    public static class LocalizationInspectorExtension
    {
        private static readonly (string label, int value)[] FlagDefs = new[]
        {
            ("Text",    (int)LocalizedAssetFlags.Text),
            ("Audio",   (int)LocalizedAssetFlags.Audio),
            ("Sprite",  (int)LocalizedAssetFlags.Sprite),
            ("Texture", (int)LocalizedAssetFlags.Texture),
            ("Video",   (int)LocalizedAssetFlags.Video),
            ("Font",    (int)LocalizedAssetFlags.Font),
        };

        static LocalizationInspectorExtension()
        {
            InspectorExtensionRegistry.RegisterNodeSection(BuildNodeSection);
            InspectorExtensionRegistry.RegisterGraphSection(BuildGraphSection);
        }

        private static void BuildNodeSection(BaseNodeData node, VisualElement parent,
            BaseGraph graph, Action markDirty)
        {
            if (node == null) return;
            if (!(graph is ILocalizedGraph localized)) return;   // only graphs that opt in carry localization flags
            var locData = localized.LocalizationFlags;

            var container = new Foldout { text = "Localized Assets", value = false };
            container.style.marginTop = 4;

            var currentFlags = (int)locData.GetFlags(node.Id);

            foreach (var (label, value) in FlagDefs)
            {
                var toggle = new Toggle(label) { value = (currentFlags & value) != 0 };
                int flagValue = value;
                toggle.RegisterValueChangedCallback(e =>
                {
                    var flags = (int)locData.GetFlags(node.Id);
                    flags = e.newValue ? flags | flagValue : flags & ~flagValue;
                    locData.SetFlags(node.Id, (LocalizedAssetFlags)flags);
                    EditorUtility.SetDirty(graph);   // flags are embedded on the graph asset now
                });
                container.Add(toggle);
            }

            parent.Add(container);
        }

        private static void BuildGraphSection(BaseGraph graph, VisualElement parent, Action markDirty)
        {
            if (!(graph is ILocalizedGraph localized)) return;
            var locData = localized.LocalizationFlags;

            var foldout = new Foldout { text = "Localization (Graph)", value = true };
            foldout.style.marginTop = 4;

            var currentDefault = (int)locData.DefaultFlags;

            foreach (var (label, value) in FlagDefs)
            {
                var toggle = new Toggle(label) { value = (currentDefault & value) != 0 };
                int flagValue = value;
                toggle.RegisterValueChangedCallback(e =>
                {
                    var flags = (int)locData.DefaultFlags;
                    locData.DefaultFlags = (LocalizedAssetFlags)(e.newValue ? flags | flagValue : flags & ~flagValue);
                    EditorUtility.SetDirty(graph);
                });
                foldout.Add(toggle);
            }

            var applyBtn = new Button(() =>
            {
                var nodeIds = new List<string>();
                foreach (var n in graph.Nodes)
                    if (n != null && !string.IsNullOrEmpty(n.Id)) nodeIds.Add(n.Id);
                locData.ApplyDefaultToAll(nodeIds);
                EditorUtility.SetDirty(graph);
                Debug.Log($"[GraphLocalization] Applied {locData.DefaultFlags} to {nodeIds.Count} nodes.");
            })
            { text = "Apply to all nodes" };
            applyBtn.style.marginTop = 4;
            foldout.Add(applyBtn);

            parent.Add(foldout);
        }
    }
}
