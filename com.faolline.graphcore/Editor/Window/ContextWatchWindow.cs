using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Live inspector for the running <see cref="BaseContext"/> — shows all typed parameters and
    /// collections in real-time during Play Mode. Discovers active contexts via
    /// <see cref="GraphRunMonitor"/> and <see cref="GraphRunContextRegistry"/>.
    /// </summary>
    public class ContextWatchWindow : EditorWindow
    {
        private int _selectedProbe;
        private Vector2 _scrollParams;
        private Vector2 _scrollCollections;

        [MenuItem("Window/Faolline/Context Watch")]
        public static void Open() => GetWindow<ContextWatchWindow>("Context Watch");

        private void OnEnable()
        {
            GraphRunMonitor.Changed += OnMonitorChanged;
        }

        private void OnDisable()
        {
            GraphRunMonitor.Changed -= OnMonitorChanged;
        }

        private void OnMonitorChanged() => Repaint();

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to watch a live context.", MessageType.Info);
                return;
            }

            var probes = GraphRunMonitor.Probes;
            if (probes == null || probes.Count == 0)
            {
                EditorGUILayout.HelpBox("No active graph runner detected.", MessageType.Info);
                return;
            }

            // Probe selector
            var names = new string[probes.Count];
            for (int i = 0; i < probes.Count; i++)
                names[i] = $"Probe {i}";
            _selectedProbe = Mathf.Clamp(_selectedProbe, 0, probes.Count - 1);
            _selectedProbe = EditorGUILayout.Popup("Active Runner", _selectedProbe, names);

            var ctx = GraphRunContextRegistry.GetContext(probes[_selectedProbe]);
            if (ctx == null)
            {
                EditorGUILayout.HelpBox("No context registered for this probe.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);

            // Parameters
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            _scrollParams = EditorGUILayout.BeginScrollView(_scrollParams, GUILayout.MaxHeight(300));
            var allParams = ctx.GetAllParameters();
            if (allParams.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                foreach (var kvp in allParams)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(200));
                    EditorGUILayout.LabelField(TypeLabel(kvp.Value), GUILayout.Width(60));
                    EditorGUILayout.LabelField(ValueLabel(kvp.Value));
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            // Collections
            EditorGUILayout.LabelField("Collections", EditorStyles.boldLabel);
            _scrollCollections = EditorGUILayout.BeginScrollView(_scrollCollections, GUILayout.MaxHeight(300));
            var allCollections = ctx.GetAllCollections();
            if (allCollections.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                foreach (var kvp in allCollections)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(200));
                    EditorGUILayout.LabelField(string.Join(", ", kvp.Value));
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static string TypeLabel(object value)
        {
            if (value is bool) return "bool";
            if (value is int) return "int";
            if (value is float) return "float";
            if (value is string) return "string";
            if (value is Vector2) return "vec2";
            if (value is Vector3) return "vec3";
            if (value is Color) return "color";
            return value?.GetType().Name ?? "null";
        }

        private static string ValueLabel(object value)
        {
            if (value is float f) return f.ToString("F3");
            if (value is Color c) return $"({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";
            return value?.ToString() ?? "null";
        }
    }
}
