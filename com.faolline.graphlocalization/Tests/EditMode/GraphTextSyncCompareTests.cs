using System.Collections.Generic;
using System.Linq;
using Faolline.GraphLocalization.Editor;
using NUnit.Framework;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// 054 detection: a graph text (the adapter's DefaultHint) is compared with the source-language cell of the
    /// table the build files its key under. The table is the reference; a difference means the graph is out of
    /// date. Pure — no asset, no settings.
    /// </summary>
    public class GraphTextSyncCompareTests
    {
        private const string Lib = "Lib";

        private static LocalizationDatabase Db(out LocalizationGraphEntry graph)
        {
            var db = new LocalizationDatabase();
            graph = db.GetOrCreateGraphEntry("guid-intro", "Intro");
            return db;
        }

        private static Dictionary<string, string> Cells(params (string key, string value)[] cells)
            => cells.ToDictionary(c => c.key, c => c.value);

        [Test]
        public void SameText_IsNoDrift()
        {
            var db = Db(out var g);
            g.AddKey("line_a", LocalizationKeyType.Text, nodeId: "a", defaultHint: "Bonjour");

            Assert.IsEmpty(GraphTextSync.Compare(Lib, db, Cells(("line_a", "Bonjour"))));
        }

        [Test]
        public void DifferentText_IsOneDrift_CarryingWhereAndBothTexts()
        {
            var db = Db(out var g);
            g.AddKey("line_a", LocalizationKeyType.Text, nodeId: "a", defaultHint: "Bonjour");

            var drift = GraphTextSync.Compare(Lib, db, Cells(("line_a", "Bonjour, voyageur"))).Single();

            Assert.AreEqual(Lib, drift.LibName);
            Assert.AreEqual("guid-intro", drift.GraphGuid);
            Assert.AreEqual("Intro", drift.GraphName);
            Assert.AreEqual(LocalizationTableNames.ForGraph("Intro"), drift.Table);
            Assert.AreEqual("line_a", drift.Key);
            Assert.AreEqual(LocalizationKeyType.Text, drift.Type);
            Assert.AreEqual("a", drift.NodeId);
            Assert.AreEqual("Bonjour", drift.GraphText);
            Assert.AreEqual("Bonjour, voyageur", drift.TableText);
            Assert.IsFalse(drift.IsGlobal);
        }

        [Test]
        public void SurroundingWhitespaceAndLineEndings_AreNotADrift()
        {
            var db = Db(out var g);
            g.AddKey("line_a", LocalizationKeyType.Text, defaultHint: "Ligne un\nLigne deux");

            Assert.IsEmpty(GraphTextSync.Compare(Lib, db, Cells(("line_a", "  Ligne un\r\nLigne deux \r\n"))));
        }

        [Test]
        public void TableText_IsReportedWithNormalizedLineEndings()
        {
            var db = Db(out var g);
            g.AddKey("line_a", LocalizationKeyType.Text, defaultHint: "old");

            var drift = GraphTextSync.Compare(Lib, db, Cells(("line_a", "new\r\nline"))).Single();

            Assert.AreEqual("new\nline", drift.TableText);
        }

        [Test]
        public void EmptyCellOrMissingKey_IsNotADrift_TheBuildFillsIt()
        {
            var db = Db(out var g);
            g.AddKey("line_a", LocalizationKeyType.Text, defaultHint: "Bonjour");
            g.AddKey("line_b", LocalizationKeyType.Text, defaultHint: "Salut");

            Assert.IsEmpty(GraphTextSync.Compare(Lib, db, Cells(("line_a", ""))));
        }

        [Test]
        public void EmptyGraphText_WithAFilledCell_IsADrift()
        {
            var db = Db(out var g);
            g.AddKey("line_a", LocalizationKeyType.Text, defaultHint: "");

            var drift = GraphTextSync.Compare(Lib, db, Cells(("line_a", "Texte importé"))).Single();

            Assert.AreEqual(string.Empty, drift.GraphText);
            Assert.AreEqual("Texte importé", drift.TableText);
        }

        [Test]
        public void GlobalKey_IsComparedAgainstItsGroupTable()
        {
            var db = new LocalizationDatabase();
            db.AddGlobalKey("speaker_mayor", LocalizationKeyType.SpeakerName, "Maire", "Speakers_Chapitre1");

            var drift = GraphTextSync.Compare(Lib, db, Cells(("speaker_mayor", "Le Maire"))).Single();

            Assert.IsTrue(drift.IsGlobal);
            Assert.IsNull(drift.GraphGuid);
            Assert.AreEqual(LocalizationTableNames.ForGroup(Lib, "Speakers_Chapitre1"), drift.Table);
            Assert.AreEqual(LocalizationKeyType.SpeakerName, drift.Type);
        }

        [Test]
        public void DriftsFollowTheBuildOrder_GraphsThenGlobalGroups()
        {
            var db = new LocalizationDatabase();
            db.AddGlobalKey("speaker_x", LocalizationKeyType.SpeakerName, "X");
            db.GetOrCreateGraphEntry("g1", "One").AddKey("line_1", LocalizationKeyType.Text, defaultHint: "a");
            db.GetOrCreateGraphEntry("g2", "Two").AddKey("choice_2", LocalizationKeyType.ChoiceLabel, defaultHint: "b");

            var keys = GraphTextSync.Compare(Lib, db,
                Cells(("line_1", "A"), ("choice_2", "B"), ("speaker_x", "Y"))).Select(d => d.Key).ToList();

            CollectionAssert.AreEqual(new[] { "line_1", "choice_2", "speaker_x" }, keys);
        }

        [Test]
        public void NullInputs_AreNoDrift()
        {
            Assert.IsEmpty(GraphTextSync.Compare(Lib, null, Cells()));
            Assert.IsEmpty(GraphTextSync.Compare(Lib, new LocalizationDatabase(), null));
        }

        // ── report ──────────────────────────────────────────────────────────────

        [Test]
        public void FormatReport_NoDrift_IsNull()
        {
            Assert.IsNull(GraphTextSync.FormatReport(Lib, new List<GraphTextDrift>()));
            Assert.IsNull(GraphTextSync.FormatReport(Lib, null));
        }

        [Test]
        public void FormatReport_NamesCountWhereBothTextsAndTheFix()
        {
            var drifts = new List<GraphTextDrift>
            {
                new GraphTextDrift(Lib, "g", "Intro", "Intro", "line_a", LocalizationKeyType.Text, "a", "Bonjour", "Salut"),
            };

            var report = GraphTextSync.FormatReport(Lib, drifts);

            StringAssert.Contains("[Lib]", report);
            StringAssert.Contains("1 graph text", report);
            StringAssert.Contains("Intro / line_a", report);
            StringAssert.Contains("Bonjour", report);
            StringAssert.Contains("Salut", report);
            StringAssert.Contains("table is the reference", report);
            StringAssert.Contains("Update Graph Texts From Tables", report);
        }

        [Test]
        public void FormatReport_ListsAFewEntries_ThenCountsTheRest()
        {
            var drifts = Enumerable.Range(0, 8)
                .Select(i => new GraphTextDrift(Lib, "g", "G", "G", "line_" + i, LocalizationKeyType.Text, "", "a", "b"))
                .ToList();

            var report = GraphTextSync.FormatReport(Lib, drifts);

            StringAssert.Contains("8 graph texts", report);
            StringAssert.Contains("line_4", report);
            StringAssert.DoesNotContain("line_5", report);
            StringAssert.Contains("3 more", report);
        }
    }
}
