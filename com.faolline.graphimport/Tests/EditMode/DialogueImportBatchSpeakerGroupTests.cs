using System;
using System.Collections.Generic;
using System.IO;
using Faolline.GraphDialogue;
using NUnit.Framework;
using UnityEditor;

namespace Faolline.GraphImport.Editor.Tests
{
    /// <summary>
    /// 052 US4 end to end through the batch entry point: -speakerTablesCsv sets created speakers' groups, realigns
    /// existing referenced speakers even when every dialogue collides on re-import, and an invalid mapping aborts
    /// before anything is written. Without the flag the import is unchanged.
    /// </summary>
    public class DialogueImportBatchSpeakerGroupTests
    {
        const string ScratchFolder = "Assets/GraphImportTestScratch";
        private string _suffix;
        private string _jsonPath;
        private string _mappingPath;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
                AssetDatabase.CreateFolder("Assets", "GraphImportTestScratch");
            _suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            _jsonPath = Path.Combine(Path.GetTempPath(), $"dialogues_{_suffix}.json");
            _mappingPath = Path.Combine(Path.GetTempPath(), $"speaker-tables_{_suffix}.csv");
            File.WriteAllText(_jsonPath, Json());
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ScratchFolder);
            if (File.Exists(_jsonPath)) File.Delete(_jsonPath);
            if (File.Exists(_mappingPath)) File.Delete(_mappingPath);
        }

        private string Key(string local) => $"K{local}_{_suffix}";

        // Two dialogues, three speaker keys (A and B in the first, C in the second).
        private string Json() =>
            "{\"dialogues\":[" +
            $"{{\"id\":\"D1_{_suffix}\",\"name\":\"D1\",\"entryNodeId\":\"n1\",\"nodes\":[" +
            $"{{\"id\":\"n1\",\"kind\":\"line\",\"speakerKey\":\"{Key("A")}\",\"text\":\"Hi\",\"next\":\"n2\"}}," +
            $"{{\"id\":\"n2\",\"kind\":\"line\",\"speakerKey\":\"{Key("B")}\",\"text\":\"Yo\",\"next\":\"n3\"}}," +
            "{\"id\":\"n3\",\"kind\":\"end\",\"reason\":\"Completed\"}]}," +
            $"{{\"id\":\"D2_{_suffix}\",\"name\":\"D2\",\"entryNodeId\":\"m1\",\"nodes\":[" +
            $"{{\"id\":\"m1\",\"kind\":\"line\",\"speakerKey\":\"{Key("C")}\",\"text\":\"Hey\",\"next\":\"m2\"}}," +
            "{\"id\":\"m2\",\"kind\":\"end\",\"reason\":\"Completed\"}]}" +
            "]}";

        private Dictionary<string, string> Args(bool withMapping)
        {
            var args = new Dictionary<string, string>
            {
                ["-dialoguesJson"] = _jsonPath,
                ["-dialoguePathTemplate"] = ScratchFolder + "/{id}.asset",
                ["-speakerFolder"] = ScratchFolder + "/Speakers",
            };
            if (withMapping) args["-speakerTablesCsv"] = _mappingPath;
            return args;
        }

        private int Run(Dictionary<string, string> args, out string output, out string error)
        {
            var o = new StringWriter();
            var e = new StringWriter();
            var code = DialogueImportBatch.Execute(args, o, e);
            output = o.ToString();
            error = e.ToString();
            return code;
        }

        private Speaker FindSpeaker(string speakerId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Speaker", new[] { ScratchFolder }))
            {
                var s = AssetDatabase.LoadAssetAtPath<Speaker>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.SpeakerId == speakerId) return s;
            }
            return null;
        }

        [Test]
        public void FirstRun_CreatedSpeakersTakeTheMappedGroups()
        {
            File.WriteAllText(_mappingPath, $"SpeakerKey,Table\n{Key("A")},Chapitre1\n{Key("B")},Chapitre2\n");

            Assert.AreEqual(0, Run(Args(true), out _, out var error), error);
            Assert.AreEqual("Chapitre1", FindSpeaker(Key("A")).LocalizationGroup);
            Assert.AreEqual("Chapitre2", FindSpeaker(Key("B")).LocalizationGroup);
            Assert.AreEqual(string.Empty, FindSpeaker(Key("C")).LocalizationGroup, "unlisted → no group");
        }

        [Test]
        public void ReImport_EveryDialogueCollides_MappingStillWins_AndIsReported()
        {
            File.WriteAllText(_mappingPath, $"SpeakerKey,Table\n{Key("A")},Chapitre1\n");
            Assert.AreEqual(0, Run(Args(true), out _, out var firstError), firstError);

            File.WriteAllText(_mappingPath, $"SpeakerKey,Table\n{Key("A")},Chapitre3\n");
            var code = Run(Args(true), out var output, out _);

            Assert.AreEqual(1, code, "the collisions alone drive the exit code, exactly as before");
            Assert.AreEqual("Chapitre3", FindSpeaker(Key("A")).LocalizationGroup);
            StringAssert.Contains($"Speaker '{Key("A")}' group 'Chapitre1' → 'Chapitre3'", output);
        }

        [Test]
        public void InvalidMapping_AbortsBeforeAnyWrite()
        {
            File.WriteAllText(_mappingPath, $"SpeakerKey,Table\n{Key("A")},Chapitre1\n{Key("A")},Chapitre2\n");

            var code = Run(Args(true), out _, out var error);

            Assert.AreEqual(1, code);
            StringAssert.Contains(Key("A"), error);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<DialogueGraph>($"{ScratchFolder}/D1_{_suffix}.asset"));
            Assert.IsNull(FindSpeaker(Key("A")));
        }

        [Test]
        public void MissingMappingFile_AbortsBeforeAnyWrite()
        {
            var code = Run(Args(true), out _, out var error);

            Assert.AreEqual(1, code);
            StringAssert.Contains(_mappingPath, error);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<DialogueGraph>($"{ScratchFolder}/D1_{_suffix}.asset"));
        }

        [Test]
        public void WithoutMapping_ImportIsUnchanged()
        {
            Assert.AreEqual(0, Run(Args(false), out _, out var error), error);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<DialogueGraph>($"{ScratchFolder}/D1_{_suffix}.asset"));
            Assert.AreEqual(string.Empty, FindSpeaker(Key("A")).LocalizationGroup);
        }
    }
}
