using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Faolline.GraphTest.Tests
{
    /// <summary>
    /// Locks the ecosystem's assembly dependency matrix (see <c>ARCHITECTURE.md</c> at the repo root).
    /// The asmdef references already make an illegal <c>using</c> a compile error; this test makes adding
    /// an illegal asmdef <em>reference</em> a test failure, so the tier rules (verticals never reference
    /// each other, external dependencies live only in adapter assemblies, Runtime never references Editor)
    /// survive future edits. Adding a new assembly or a new edge is fine — declare it here on purpose and
    /// update <c>ARCHITECTURE.md</c> in the same commit.
    /// </summary>
    [TestFixture]
    public class DependencyMatrixTests
    {
        // ── Ecosystem assembly names ────────────────────────────────────────────
        private const string CoreRuntime         = "com.faolline.graphcore.Runtime";
        private const string CoreRuntimeCore     = "com.faolline.graphcore.Runtime.Core";
        private const string CoreEditor          = "com.faolline.graphcore.Editor";
        private const string CoreTests           = "com.faolline.graphcore.Tests.EditMode";
        private const string LocRuntime          = "com.faolline.graphlocalization.Runtime";
        private const string LocEditor           = "com.faolline.graphlocalization.Editor";
        private const string LocUnity            = "com.faolline.graphlocalization.Localization.Unity";
        private const string LocUnityEditor      = "com.faolline.graphlocalization.Localization.Unity.Editor";
        private const string LocTests            = "com.faolline.graphlocalization.Tests.EditMode";
        private const string LogRuntime          = "com.faolline.graphlogging.Runtime";
        private const string LogEditor           = "com.faolline.graphlogging.Editor";
        private const string LogTests            = "com.faolline.graphlogging.Tests.EditMode";
        private const string StdRuntime          = "com.faolline.graphstandard.Runtime";
        private const string StdEditor           = "com.faolline.graphstandard.Editor";
        private const string StdTests            = "com.faolline.graphstandard.Tests.EditMode";
        private const string SaveRuntime         = "com.faolline.graphsave.Runtime";
        private const string SaveTests           = "com.faolline.graphsave.Tests.EditMode";
        private const string SaveBridgeRuntime   = "com.faolline.graphsave.savesystem.Runtime";
        private const string SaveBridgeTests     = "com.faolline.graphsave.savesystem.Tests.EditMode";
        private const string AddrBridgeRuntime   = "com.faolline.graphgameflow.addressables.Runtime";
        private const string AddrBridgeEditor    = "com.faolline.graphgameflow.addressables.Editor";
        private const string AddrBridgeTests     = "com.faolline.graphgameflow.addressables.Tests.EditMode";
        private const string AddrBridgeTestsPlay = "com.faolline.graphgameflow.addressables.Tests.PlayMode";
        private const string DialogueRuntime     = "com.faolline.graphdialoguesystem.Runtime";
        private const string DialogueEditor      = "com.faolline.graphdialoguesystem.Editor";
        private const string DialogueLocUnity    = "com.faolline.graphdialoguesystem.Localization.Unity";
        private const string DialogueUI          = "com.faolline.graphdialoguesystem.UI";
        private const string DialogueTests       = "com.faolline.graphdialoguesystem.Tests.EditMode";
        private const string DialogueTestsPlay   = "com.faolline.graphdialoguesystem.Tests.PlayMode";
        private const string DialogueUITests     = "com.faolline.graphdialoguesystem.UI.Tests.EditMode";
        private const string DialogueUITestsPlay = "com.faolline.graphdialoguesystem.UI.Tests.PlayMode";
        private const string QuestRuntime        = "com.faolline.graphquest.Runtime";
        private const string QuestEditor         = "com.faolline.graphquest.Editor";
        private const string QuestTests          = "com.faolline.graphquest.Tests.EditMode";
        private const string FlowRuntime         = "com.faolline.graphgameflow.Runtime";
        private const string FlowEditor          = "com.faolline.graphgameflow.Editor";
        private const string FlowTests           = "com.faolline.graphgameflow.Tests.EditMode";
        private const string FlowTestsPlay       = "com.faolline.graphgameflow.Tests.PlayMode";
        private const string ImportRuntime       = "com.faolline.graphimport.Runtime";
        private const string ImportEditor        = "com.faolline.graphimport.Editor";
        private const string ImportTests         = "com.faolline.graphimport.Tests.EditMode";
        private const string GraphTestRuntime    = "com.faolline.graphTest.Runtime";
        private const string GraphTestEditor     = "com.faolline.graphTest.Editor";
        private const string GraphTestTests      = "com.faolline.graphTest.Tests.EditMode";
        private const string StarterRuntime      = "com.faolline.starterGraph.Runtime";
        private const string StarterEditor       = "com.faolline.starterGraph.Editor";
        private const string StarterTests        = "com.faolline.starterGraph.Tests.EditMode";

        // ── External assembly names (Unity + UnitySaveSystem) ───────────────────
        private const string TestRunner        = "UnityEngine.TestRunner";
        private const string TestRunnerEditor  = "UnityEditor.TestRunner";
        private const string UnityLoc          = "Unity.Localization";
        private const string UnityLocEditor    = "Unity.Localization.Editor";
        private const string UnityResourceMgr  = "Unity.ResourceManager";
        private const string TextMeshPro       = "Unity.TextMeshPro";
        private const string UGui              = "UnityEngine.UI";
        private const string InputSystem       = "Unity.InputSystem";
        private const string SaveSystemCore    = "SaveSystemCore";
        private const string SaveSystemJson    = "SaveSystemJson";
        private const string UnityAddr         = "Unity.Addressables";
        private const string UnityAddrEditor   = "Unity.Addressables.Editor";

        /// <summary>
        /// The allowed reference set per assembly (the architecture, as data). A reference absent from an
        /// assembly's set fails <see cref="NoAssemblyReferencesOutsideItsAllowedSet"/>. Sets are upper
        /// bounds — removing a reference from an asmdef never fails this test.
        /// </summary>
        private static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]>
        {
            // ── Tier 0 · Foundation (no references at all, except graphlogging which every other
            //    T0 member may share as a leaf utility) ───────────────────────────
            [CoreRuntime] = new[] { CoreRuntimeCore, LogRuntime },
            [CoreRuntimeCore] = new string[0],
            [LocRuntime]  = new[] { LogRuntime },
            [LogRuntime]  = new string[0],

            // ── Tier 1 · Neutral capabilities (foundation only) ─────────────────
            [StdRuntime]  = new[] { CoreRuntime, CoreRuntimeCore, LogRuntime },
            [SaveRuntime] = new[] { CoreRuntime, CoreRuntimeCore, LogRuntime },

            // ── Tier 2 · Verticals (tiers 0–1 only, never another vertical) ─────
            [DialogueRuntime] = new[] { CoreRuntime, CoreRuntimeCore, LocRuntime, LogRuntime },
            [QuestRuntime]    = new[] { CoreRuntime, CoreRuntimeCore, StdRuntime, LocRuntime, LogRuntime },
            [FlowRuntime]     = new[] { CoreRuntime, CoreRuntimeCore, SaveRuntime, LogRuntime },

            // ── Tier 3 · Adapters (the only runtime assemblies with external refs) ──
            [LocUnity]          = new[] { LocRuntime, LogRuntime, UnityLoc, UnityResourceMgr },
            [DialogueLocUnity]  = new[] { DialogueRuntime, UnityLoc },
            [DialogueUI]        = new[] { CoreRuntime, CoreRuntimeCore, DialogueRuntime, LocRuntime, LogRuntime, TextMeshPro, UGui, InputSystem },
            [SaveBridgeRuntime] = new[] { SaveRuntime, LogRuntime, SaveSystemCore },
            [AddrBridgeRuntime] = new[] { FlowRuntime, CoreRuntime, CoreRuntimeCore, LogRuntime, UnityAddr, UnityResourceMgr },

            // ── Editor assemblies (own package + upstream Runtime/Editor pairs) ─
            [CoreEditor]     = new[] { CoreRuntime, CoreRuntimeCore, LogRuntime },
            [LocEditor]      = new[] { LocRuntime, CoreRuntime, CoreRuntimeCore, CoreEditor, LogRuntime },
            [LocUnityEditor] = new[] { LocRuntime, LocEditor, LocUnity, LogRuntime, UnityLoc, UnityLocEditor },
            [LogEditor]      = new[] { LogRuntime },
            [StdEditor]      = new[] { CoreRuntime, CoreRuntimeCore, StdRuntime, LogRuntime },
            [DialogueEditor] = new[] { CoreRuntime, CoreRuntimeCore, CoreEditor, DialogueRuntime, LocRuntime, LocEditor, LogRuntime },
            [QuestEditor]    = new[] { CoreRuntime, CoreRuntimeCore, CoreEditor, QuestRuntime, StdRuntime, LocRuntime, LocEditor, LogRuntime },
            [FlowEditor]     = new[] { CoreRuntime, CoreRuntimeCore, CoreEditor, FlowRuntime, LogRuntime },
            [AddrBridgeEditor] = new[] { CoreRuntime, CoreRuntimeCore, FlowEditor, LogRuntime, UnityAddrEditor },

            // ── Tier 4 · Generation tooling (Editor-only; may reference several verticals AT ONCE
            //    because it never executes a graph — only authors assets via their public builder
            //    APIs; one-way — nothing in T0–T3 may reference it back) ──────────
            [ImportRuntime] = new string[0],
            [ImportEditor]  = new[]
            {
                ImportRuntime, CoreRuntime, CoreRuntimeCore, CoreEditor,
                StdRuntime, StdEditor,
                QuestRuntime, QuestEditor,
                FlowRuntime, FlowEditor,
                DialogueRuntime, LogRuntime,
            },

            // ── Dev tooling (internal-only packages) ────────────────────────────
            [GraphTestRuntime] = new[] { CoreRuntime, CoreRuntimeCore, StdRuntime, LogRuntime },
            [GraphTestEditor]  = new[] { GraphTestRuntime, CoreRuntime, CoreRuntimeCore, CoreEditor, LogRuntime },
            [StarterRuntime]   = new[] { CoreRuntime, CoreRuntimeCore },
            [StarterEditor]    = new[] { CoreRuntime, CoreRuntimeCore, CoreEditor, StarterRuntime, LogRuntime },

            // ── Test assemblies ─────────────────────────────────────────────────
            [CoreTests]           = new[] { CoreRuntime, CoreRuntimeCore, CoreEditor, TestRunner, TestRunnerEditor },
            [LocTests]            = new[] { LocRuntime, LocEditor, TestRunner, TestRunnerEditor },
            [LogTests]            = new[] { LogRuntime, LogEditor, TestRunner, TestRunnerEditor },
            [StdTests]            = new[] { StdRuntime, StdEditor, CoreRuntime, CoreRuntimeCore, TestRunner, TestRunnerEditor },
            [SaveTests]           = new[] { SaveRuntime, CoreRuntime, CoreRuntimeCore, TestRunner, TestRunnerEditor },
            [SaveBridgeTests]     = new[] { SaveBridgeRuntime, SaveRuntime, CoreRuntime, CoreRuntimeCore, SaveSystemCore, SaveSystemJson, TestRunner, TestRunnerEditor },
            [AddrBridgeTests]     = new[] { AddrBridgeRuntime, AddrBridgeEditor, FlowRuntime, FlowEditor, CoreRuntime, CoreRuntimeCore, UnityAddr, UnityAddrEditor, UnityResourceMgr, TestRunner, TestRunnerEditor },
            [AddrBridgeTestsPlay] = new[] { AddrBridgeRuntime, FlowRuntime, CoreRuntime, CoreRuntimeCore, UnityAddr, UnityAddrEditor, UnityResourceMgr, TestRunner },
            [DialogueTests]       = new[] { DialogueRuntime, DialogueEditor, CoreRuntime, CoreRuntimeCore, CoreEditor, LocRuntime, LocEditor, TestRunner, TestRunnerEditor },
            [DialogueTestsPlay]   = new[] { DialogueRuntime, CoreRuntime, CoreRuntimeCore, LocRuntime, TestRunner, TestRunnerEditor },
            [DialogueUITests]     = new[] { DialogueUI, DialogueRuntime, CoreRuntime, CoreRuntimeCore, LocRuntime, TextMeshPro, UGui, TestRunner, TestRunnerEditor },
            [DialogueUITestsPlay] = new[] { DialogueUI, DialogueRuntime, CoreRuntime, CoreRuntimeCore, LocRuntime, TextMeshPro, UGui, TestRunner, TestRunnerEditor },
            [QuestTests]          = new[] { QuestRuntime, QuestEditor, CoreRuntime, CoreRuntimeCore, CoreEditor, StdRuntime, LocRuntime, SaveRuntime, TestRunner, TestRunnerEditor },
            [FlowTests]           = new[] { FlowRuntime, FlowEditor, CoreRuntime, CoreRuntimeCore, CoreEditor, SaveRuntime, TestRunner, TestRunnerEditor },
            [FlowTestsPlay]       = new[] { FlowRuntime, CoreRuntime, CoreRuntimeCore, TestRunner },
            [ImportTests]         = new[]
            {
                ImportRuntime, ImportEditor, CoreRuntime, CoreRuntimeCore, CoreEditor,
                StdRuntime, StdEditor, QuestRuntime, QuestEditor, FlowRuntime, FlowEditor, DialogueRuntime,
                TestRunner, TestRunnerEditor,
            },
            [GraphTestTests]      = new[] { GraphTestRuntime, GraphTestEditor, CoreRuntime, CoreRuntimeCore, CoreEditor, TestRunner, TestRunnerEditor },
            [StarterTests]        = new[] { StarterRuntime, StarterEditor, CoreRuntime, CoreRuntimeCore, CoreEditor, TestRunner, TestRunnerEditor },
        };

        // ── Discovery ───────────────────────────────────────────────────────────

        [Serializable]
        private class AsmdefDto
        {
            public string   name;
            public string[] references;
        }

        /// <summary>
        /// All ecosystem asmdefs found in the project, as name → resolved reference names. Ecosystem =
        /// the com.faolline.graph* / com.faolline.starterGraph packages; the external
        /// com.faolline.savesystem.* packages (UnitySaveSystem) are deliberately out of scope.
        /// </summary>
        private static Dictionary<string, string[]> FindEcosystemAssemblies()
        {
            var found = new Dictionary<string, string[]>();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.EndsWith(".asmdef", StringComparison.Ordinal)) continue;
                if (!path.Contains("com.faolline.graph") && !path.Contains("com.faolline.starterGraph")) continue;

                var dto = ParseAsmdef(path);
                found[dto.name] = (dto.references ?? new string[0]).Select(ResolveReferenceName).ToArray();
            }
            return found;
        }

        private static AsmdefDto ParseAsmdef(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assetPath);
            Assert.IsNotNull(asset, $"Could not load asmdef at '{assetPath}'.");
            return JsonUtility.FromJson<AsmdefDto>(asset.text);
        }

        /// <summary>Resolves a "GUID:…" asmdef reference to its assembly name; passes names through.</summary>
        private static string ResolveReferenceName(string reference)
        {
            if (!reference.StartsWith("GUID:", StringComparison.Ordinal)) return reference;

            var path = AssetDatabase.GUIDToAssetPath(reference.Substring("GUID:".Length));
            // An unresolvable GUID stays as-is and surfaces as a disallowed reference below.
            return string.IsNullOrEmpty(path) ? reference : ParseAsmdef(path).name;
        }

        // ── Tests ───────────────────────────────────────────────────────────────

        [Test]
        public void EveryEcosystemAssemblyIsDeclaredInTheMatrix()
        {
            var undeclared = FindEcosystemAssemblies().Keys.Where(n => !Allowed.ContainsKey(n)).ToList();

            Assert.IsEmpty(undeclared,
                "New ecosystem assemblies must be placed in the dependency matrix on purpose. " +
                "Add them to DependencyMatrixTests.Allowed (respecting the tier rules) and to ARCHITECTURE.md:\n - " +
                string.Join("\n - ", undeclared));
        }

        [Test]
        public void EveryDeclaredAssemblyExistsOnDisk()
        {
            var found   = FindEcosystemAssemblies();
            var missing = Allowed.Keys.Where(n => !found.ContainsKey(n)).ToList();

            Assert.IsEmpty(missing,
                "Stale matrix entries (renamed or deleted assemblies?). " +
                "Update DependencyMatrixTests.Allowed and ARCHITECTURE.md:\n - " +
                string.Join("\n - ", missing));
        }

        [Test]
        public void NoAssemblyReferencesOutsideItsAllowedSet()
        {
            var violations = new List<string>();
            foreach (var pair in FindEcosystemAssemblies())
            {
                if (!Allowed.TryGetValue(pair.Key, out var allowed)) continue; // reported by the test above

                foreach (var reference in pair.Value.Where(r => !allowed.Contains(r)))
                    violations.Add($"{pair.Key} → {reference}");
            }

            Assert.IsEmpty(violations,
                "Assembly references outside the declared dependency matrix. If the edge is intended, " +
                "add it to DependencyMatrixTests.Allowed AND ARCHITECTURE.md in the same commit — " +
                "mind the tier rules (verticals never reference verticals; external deps only in adapters):\n - " +
                string.Join("\n - ", violations));
        }
    }
}
