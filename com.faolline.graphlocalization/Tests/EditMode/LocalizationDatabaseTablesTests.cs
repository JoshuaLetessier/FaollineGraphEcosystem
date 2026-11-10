using System.Linq;
using NUnit.Framework;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// 054: <see cref="LocalizationDatabase.Tables"/> is the single definition of "which table holds which key",
    /// shared by the CSV exporter, the Unity syncer and the graph-text check: one table per graph, then one per
    /// global-key group, named by <see cref="LocalizationTableNames"/>.
    /// </summary>
    public class LocalizationDatabaseTablesTests
    {
        [Test]
        public void GraphTablesThenGroupTables_InBuildOrder()
        {
            var db = new LocalizationDatabase();
            db.GetOrCreateGraphEntry("g1", "Intro").AddKey("line_1", LocalizationKeyType.Text, defaultHint: "a");
            db.GetOrCreateGraphEntry("g2", "Chapitre 2").AddKey("line_2", LocalizationKeyType.Text, defaultHint: "b");
            db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "A");
            db.AddGlobalKey("speaker_b", LocalizationKeyType.SpeakerName, "B", "Speakers_C1");

            var tables = db.Tables("Lib");

            CollectionAssert.AreEqual(new[]
            {
                LocalizationTableNames.ForGraph("Intro"),
                LocalizationTableNames.ForGraph("Chapitre 2"),
                LocalizationTableNames.ForGroup("Lib", null),
                LocalizationTableNames.ForGroup("Lib", "Speakers_C1"),
            }, tables.Select(t => t.Table).ToArray());
        }

        [Test]
        public void GraphTable_CarriesItsGraphAndKeys_GroupTableHasNoGraph()
        {
            var db = new LocalizationDatabase();
            db.GetOrCreateGraphEntry("g1", "Intro").AddKey("line_1", LocalizationKeyType.Text, defaultHint: "a");
            db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "A", "Speakers");

            var tables = db.Tables("Lib");

            Assert.IsTrue(tables[0].IsGraphTable);
            Assert.AreEqual("g1", tables[0].GraphGuid);
            Assert.AreEqual("Intro", tables[0].GraphName);
            Assert.IsNull(tables[0].Group);
            Assert.AreEqual("line_1", tables[0].Keys.Single().Key);

            Assert.IsFalse(tables[1].IsGraphTable);
            Assert.IsNull(tables[1].GraphGuid);
            Assert.IsNull(tables[1].GraphName);
            Assert.AreEqual("Speakers", tables[1].Group);
            Assert.AreEqual("speaker_a", tables[1].Keys.Single().Key);
        }

        [Test]
        public void EmptyDatabase_HasNoTable()
        {
            Assert.IsEmpty(new LocalizationDatabase().Tables("Lib"));
        }
    }
}
