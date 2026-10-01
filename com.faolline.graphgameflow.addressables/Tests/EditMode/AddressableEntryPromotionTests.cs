using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using Faolline.GraphGameFlow.Addressables.Editor;

namespace Faolline.GraphGameFlow.Addressables.Tests
{
    /// <summary>
    /// Group placement of <see cref="AddressableEntryPromotion"/>, against a temporary (never persisted)
    /// <see cref="AddressableAssetSettings"/> so the project's own Addressables configuration is never touched.
    /// The promoted asset is this package's own PlayMode test scene, located through the AssetDatabase so the
    /// test holds both in the dev repo (<c>Assets/…</c>) and in a consumer (<c>Packages/…</c>).
    /// </summary>
    public class AddressableEntryPromotionTests
    {
        private const string Key = "PromotionTest.SceneA";

        private AddressableAssetSettings _settings;
        private AddressableAssetGroup    _zoneGroup;
        private string _scenePath;
        private string _sceneGuid;
        private string _folderGuid;

        [SetUp]
        public void SetUp()
        {
            _settings  = AddressableAssetSettings.Create("Temp/PromotionTests", "PromotionTestSettings", true, false);
            _zoneGroup = _settings.CreateGroup("Zone", false, false, false, null);

            _sceneGuid = AssetDatabase.FindAssets("AddressablesSceneA t:Scene").Single();
            _scenePath = AssetDatabase.GUIDToAssetPath(_sceneGuid);
            _folderGuid = AssetDatabase.AssetPathToGUID(Path.GetDirectoryName(_scenePath).Replace('\\', '/'));
            Assert.IsFalse(string.IsNullOrEmpty(_folderGuid), "the test scene's folder resolves to a GUID.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var group in _settings.groups.ToList()) UnityEngine.Object.DestroyImmediate(group);
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void ExistingEntry_KeepsItsGroup_OnlyAddressChanges()
        {
            var existing = _settings.CreateOrMoveEntry(_sceneGuid, _zoneGroup);
            existing.address = "old-key";

            var entry = AddressableEntryPromotion.Promote(_settings, _scenePath, Key);

            Assert.AreSame(existing, entry);
            Assert.AreSame(_zoneGroup, entry.parentGroup, "an already-grouped entry must not be moved to the default group.");
            Assert.AreEqual(Key, entry.address);
        }

        [Test]
        public void ExistingEntry_InsideFolderOfAnotherGroup_StillKeepsItsOwnGroup()
        {
            var folderGroup = _settings.CreateGroup("Folder", false, false, false, null);
            _settings.CreateOrMoveEntry(_folderGuid, folderGroup);
            _settings.CreateOrMoveEntry(_sceneGuid, _zoneGroup);

            var entry = AddressableEntryPromotion.Promote(_settings, _scenePath, Key);

            Assert.AreSame(_zoneGroup, entry.parentGroup);
        }

        [Test]
        public void NewEntry_InsideAddressableFolder_GoesToTheFolderGroup()
        {
            _settings.CreateOrMoveEntry(_folderGuid, _zoneGroup);

            var entry = AddressableEntryPromotion.Promote(_settings, _scenePath, Key);

            Assert.AreSame(_zoneGroup, entry.parentGroup);
            Assert.AreEqual(_sceneGuid, entry.guid);
            Assert.AreEqual(Key, entry.address);
        }

        [Test]
        public void NewEntry_NoAddressableFolder_FallsBackToDefaultGroup()
        {
            var entry = AddressableEntryPromotion.Promote(_settings, _scenePath, Key);

            Assert.AreSame(_settings.DefaultGroup, entry.parentGroup);
            Assert.AreNotSame(_zoneGroup, _settings.DefaultGroup);
            Assert.AreEqual(Key, entry.address);
        }

        [Test]
        public void UnknownAssetPath_ReturnsNull_CreatesNothing()
        {
            Assert.IsNull(AddressableEntryPromotion.Promote(_settings, "Assets/DoesNotExist/Nothing.asset", Key));
            Assert.IsFalse(_settings.groups.SelectMany(g => g.entries).Any());
        }

        [Test]
        public void NullSettings_ReturnsNull()
        {
            Assert.IsNull(AddressableEntryPromotion.Promote(null, _scenePath, Key));
        }
    }
}
