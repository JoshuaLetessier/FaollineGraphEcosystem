using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.Serialization;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Regression guard: fields renamed by the 0.22 asset-only refactor MUST keep their
    /// <see cref="FormerlySerializedAsAttribute"/> so assets authored before the rename still deserialize
    /// (without it they silently become null → the action is a no-op). #1 dogfood finding. This asserts the
    /// attribute contract (Unity's YAML deserializer honours it; a plain reflection check is the portable proxy).
    /// </summary>
    public class SerializedRenameCompatTests
    {
        private static void AssertFormerlyNamed(System.Type type, string fieldName, string oldName)
        {
            var f = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"{type.Name}.{fieldName} should exist");
            var oldNames = f.GetCustomAttributes<FormerlySerializedAsAttribute>().Select(a => a.oldName).ToList();
            Assert.Contains(oldName, oldNames,
                $"{type.Name}.{fieldName} must keep [FormerlySerializedAs(\"{oldName}\")] so pre-0.22 assets still deserialize.");
        }

        [Test]
        public void RaiseSignalAction_Signal_RecoversOldAssetField()
            => AssertFormerlyNamed(typeof(RaiseSignalAction), "_signal", "_signalAsset");

        [Test]
        public void AddToCollectionAction_Collection_RecoversOldAssetField()
            => AssertFormerlyNamed(typeof(AddToCollectionAction), "_collection", "_collectionAsset");

        [Test]
        public void AddToCollectionAction_Entry_RecoversOldAssetField()
            => AssertFormerlyNamed(typeof(AddToCollectionAction), "_entry", "_valueAsset");
    }
}
