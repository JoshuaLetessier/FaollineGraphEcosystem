using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Finding 23: a GUID assigned in <c>OnEnable</c> reaches memory but not disk (assigning a field in code
    /// doesn't dirty the asset), so a migrated/never-saved asset re-derives a different GUID each session —
    /// desyncing generated constants and save-file signal history across a session boundary.
    /// <see cref="StableGuidPersistence"/> forces the assignment to disk.
    /// </summary>
    public class StableGuidPersistenceTests
    {
        private const string TempFolder = "Assets/Temp_StableGuidPersistTest";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "Temp_StableGuidPersistTest");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TempFolder);

        [Test]
        public void SaveIfPersistentAsset_WritesAnInMemoryOnlyIdToDisk()
        {
            var path = TempFolder + "/Sig.asset";
            var sig = ScriptableObject.CreateInstance<SignalDef>();
            AssetDatabase.CreateAsset(sig, path);   // written with its OnEnable GUID

            // Assign _id directly (mirrors OnEnable: reaches memory, does NOT dirty the asset).
            typeof(SignalDef).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(sig, "PERSIST-ME-1234");

            var before = File.ReadAllText(Path.GetFullPath(path));
            StringAssert.DoesNotContain("PERSIST-ME-1234", before, "sanity: the direct assignment is not yet on disk");

            StableGuidPersistence.SaveIfPersistentAsset(sig);

            var after = File.ReadAllText(Path.GetFullPath(path));
            StringAssert.Contains("PERSIST-ME-1234", after, "the id must be flushed to the .asset file so it survives a session boundary");
        }

        [Test]
        public void SaveIfPersistentAsset_RuntimeInstance_IsNoOp()
        {
            // A runtime instance (SignalDef.Create, tests) is not a persistent asset — never saved, no throw.
            var sig = SignalDef.Create("runtime");
            Assert.DoesNotThrow(() => StableGuidPersistence.SaveIfPersistentAsset(sig));
            Object.DestroyImmediate(sig);
        }

        [Test]
        public void ScheduleSave_RuntimeInstance_DoesNotThrow()
        {
            var sig = SignalDef.Create("runtime");
            Assert.DoesNotThrow(() => StableGuidPersistence.ScheduleSave(sig));
            Object.DestroyImmediate(sig);
        }
    }
}
