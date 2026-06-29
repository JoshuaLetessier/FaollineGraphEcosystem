using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Generates category-coloured icons and assigns them to ScriptableObject scripts via
    /// <see cref="MonoImporter"/>, so instances show a distinctive icon in the Project browser.
    /// Run once via <c>Faolline ▸ Tools ▸ Assign Script Icons</c>; assignments persist in .meta files.
    /// </summary>
    public static class ScriptIconAssigner
    {
        private const int Size = 64;
        private const string IconFolderAssets = "Assets/FaollineGraphEcosystem/com.faolline.graphcore/Editor/Icons";
        private const string IconFolderPackage = "Packages/com.faolline.graphcore/Editor/Icons";

        private struct IconDef
        {
            public string Name;
            public Color Fill;
            public Color Border;
            public string Letter;
        }

        private static readonly Dictionary<string, IconDef> TypeToIcon = new Dictionary<string, IconDef>
        {
            // ── Graphs (each type gets its own colour + letter) ──────────────
            ["DialogueGraph"]   = new IconDef { Name = "ico_graph_dialogue",  Fill = new Color(0.20f, 0.60f, 0.85f), Border = new Color(0.10f, 0.40f, 0.65f), Letter = "D" },
            ["GameFlowGraph"]   = new IconDef { Name = "ico_graph_gameflow",  Fill = new Color(0.30f, 0.75f, 0.40f), Border = new Color(0.15f, 0.55f, 0.20f), Letter = "F" },
            ["QuestGraph"]      = new IconDef { Name = "ico_graph_quest",     Fill = new Color(0.85f, 0.65f, 0.20f), Border = new Color(0.65f, 0.45f, 0.10f), Letter = "Q" },
            ["StarterGraph"]    = new IconDef { Name = "ico_graph_starter",   Fill = new Color(0.60f, 0.60f, 0.60f), Border = new Color(0.40f, 0.40f, 0.40f), Letter = "S" },
            ["BaseGraph"]       = new IconDef { Name = "ico_graph_base",      Fill = new Color(0.50f, 0.50f, 0.70f), Border = new Color(0.35f, 0.35f, 0.55f), Letter = "G" },
            ["TestGraph"]       = new IconDef { Name = "ico_graph_test",      Fill = new Color(0.55f, 0.55f, 0.55f), Border = new Color(0.35f, 0.35f, 0.35f), Letter = "T" },

            // ── Conditions (blue family) ─────────────────────────────────────
            ["_condition"]      = new IconDef { Name = "ico_condition",       Fill = new Color(0.25f, 0.55f, 0.90f), Border = new Color(0.15f, 0.35f, 0.70f), Letter = "?" },

            // ── Actions (orange family) ──────────────────────────────────────
            ["_action"]         = new IconDef { Name = "ico_action",          Fill = new Color(0.90f, 0.55f, 0.15f), Border = new Color(0.70f, 0.40f, 0.10f), Letter = "!" },

            // ── Other SO types ───────────────────────────────────────────────
            ["Speaker"]         = new IconDef { Name = "ico_speaker",         Fill = new Color(0.70f, 0.45f, 0.80f), Border = new Color(0.50f, 0.30f, 0.60f), Letter = "🗣" },
            ["SignalName"]      = new IconDef { Name = "ico_signal",          Fill = new Color(0.90f, 0.35f, 0.35f), Border = new Color(0.70f, 0.20f, 0.20f), Letter = "⚡" },
            ["GraphTemplate"]   = new IconDef { Name = "ico_template",        Fill = new Color(0.50f, 0.70f, 0.60f), Border = new Color(0.35f, 0.50f, 0.40f), Letter = "▦" },
        };

        private static string ResolveIconFolder()
        {
            if (AssetDatabase.IsValidFolder(IconFolderAssets)) return IconFolderAssets;
            if (AssetDatabase.IsValidFolder(IconFolderPackage)) return IconFolderPackage;
            return IconFolderAssets;
        }

        [MenuItem("Faolline/Tools/Assign Script Icons")]
        public static void AssignAll()
        {
            var iconFolder = ResolveIconFolder();
            var icons = LoadOrGenerateIcons(iconFolder);
            int assigned = 0;

            var guids = AssetDatabase.FindAssets("t:MonoScript", new[]
            {
                "Assets/FaollineGraphEcosystem/com.faolline.graphcore",
                "Assets/FaollineGraphEcosystem/com.faolline.graphstandard",
                "Assets/FaollineGraphEcosystem/com.faolline.graphdialoguesystem",
                "Assets/FaollineGraphEcosystem/com.faolline.graphgameflow",
                "Assets/FaollineGraphEcosystem/com.faolline.graphquest",
                "Assets/FaollineGraphEcosystem/com.faolline.graphsave",
                "Assets/FaollineGraphEcosystem/com.faolline.starterGraph",
            });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Tests/")) continue;

                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript == null) continue;

                var scriptClass = monoScript.GetClass();
                if (scriptClass == null || !typeof(ScriptableObject).IsAssignableFrom(scriptClass)) continue;

                var iconKey = ResolveIconKey(scriptClass);
                if (iconKey == null || !icons.TryGetValue(iconKey, out var icon)) continue;

                var importer = AssetImporter.GetAtPath(path) as MonoImporter;
                if (importer == null) continue;

                importer.SetIcon(icon);
                importer.SaveAndReimport();
                assigned++;
            }

            RefreshExistingAssets();
            Debug.Log($"[GraphCore] Assigned icons to {assigned} scripts and refreshed existing assets.");
        }

        /// <summary>
        /// Reimports all existing ScriptableObject assets whose type has an icon, so the Project browser
        /// picks up the new icon even for assets created before the <c>[Icon]</c> attribute was added.
        /// </summary>
        private static void RefreshExistingAssets()
        {
            int refreshed = 0;
            var typeNames = new[] { "BaseGraph", "BaseCondition", "BaseAction", "Speaker", "SignalName", "GraphTemplate" };
            foreach (var typeName in typeNames)
            {
                var guids = AssetDatabase.FindAssets($"t:{typeName}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || path.Contains("/Tests/")) continue;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    refreshed++;
                }
            }
            if (refreshed > 0)
                Debug.Log($"[GraphCore] Reimported {refreshed} existing assets to refresh their icons.");
        }

        private static string ResolveIconKey(Type type)
        {
            if (TypeToIcon.ContainsKey(type.Name))
                return type.Name;

            if (typeof(BaseCondition).IsAssignableFrom(type))
                return "_condition";

            if (typeof(BaseAction).IsAssignableFrom(type))
                return "_action";

            return null;
        }

        private static Dictionary<string, Texture2D> LoadOrGenerateIcons(string iconFolder)
        {
            bool isWritable = iconFolder.StartsWith("Assets");
            if (isWritable) EnsureIconFolder(iconFolder);

            var result = new Dictionary<string, Texture2D>();
            foreach (var kvp in TypeToIcon)
            {
                var def = kvp.Value;
                var path = $"{iconFolder}/{def.Name}.png";

                var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (loaded == null && isWritable)
                {
                    var tex = CreateIcon(def);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.GUI;
                        importer.npotScale = TextureImporterNPOTScale.None;
                        importer.mipmapEnabled = false;
                        importer.SaveAndReimport();
                    }
                    loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }

                if (loaded != null)
                    result[kvp.Key] = loaded;
            }
            return result;
        }

        private static Texture2D CreateIcon(IconDef def)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var transparent = new Color(0, 0, 0, 0);
            float center = Size / 2f;
            float outerR = Size / 2f - 1f;
            float innerR = outerR - 3f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > outerR)
                    tex.SetPixel(x, y, transparent);
                else if (dist > innerR)
                    tex.SetPixel(x, y, def.Border);
                else
                    tex.SetPixel(x, y, def.Fill);
            }

            DrawLetter(tex, def.Letter, Color.white);
            tex.Apply();
            return tex;
        }

        private static void DrawLetter(Texture2D tex, string letter, Color color)
        {
            if (string.IsNullOrEmpty(letter)) return;

            var patterns = new Dictionary<string, bool[,]>
            {
                ["D"] = new bool[,] {
                    {true,true,true,false,false},
                    {true,false,false,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,true,false},
                    {true,true,true,false,false}
                },
                ["F"] = new bool[,] {
                    {true,true,true,true,true},
                    {true,false,false,false,false},
                    {true,false,false,false,false},
                    {true,true,true,true,false},
                    {true,false,false,false,false},
                    {true,false,false,false,false},
                    {true,false,false,false,false}
                },
                ["Q"] = new bool[,] {
                    {false,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,true,false,true},
                    {false,true,true,true,false},
                    {false,false,false,true,true}
                },
                ["S"] = new bool[,] {
                    {false,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,false},
                    {false,true,true,true,false},
                    {false,false,false,false,true},
                    {true,false,false,false,true},
                    {false,true,true,true,false}
                },
                ["G"] = new bool[,] {
                    {false,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,false},
                    {true,false,true,true,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {false,true,true,true,false}
                },
                ["T"] = new bool[,] {
                    {true,true,true,true,true},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false}
                },
                ["?"] = new bool[,] {
                    {false,true,true,true,false},
                    {true,false,false,false,true},
                    {false,false,false,true,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,false,false,false},
                    {false,false,true,false,false}
                },
                ["!"] = new bool[,] {
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,false,false,false},
                    {false,false,true,false,false}
                },
            };

            if (!patterns.TryGetValue(letter, out var pattern)) return;

            int rows = pattern.GetLength(0);
            int cols = pattern.GetLength(1);
            int pixelSize = 3;
            int startX = (Size - cols * pixelSize) / 2;
            int startY = (Size - rows * pixelSize) / 2;

            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!pattern[r, c]) continue;
                for (int py = 0; py < pixelSize; py++)
                for (int px = 0; px < pixelSize; px++)
                {
                    int x = startX + c * pixelSize + px;
                    int y = startY + (rows - 1 - r) * pixelSize + py;
                    if (x >= 0 && x < Size && y >= 0 && y < Size)
                        tex.SetPixel(x, y, color);
                }
            }
        }

        private static void EnsureIconFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, "Icons");
        }
    }
}
