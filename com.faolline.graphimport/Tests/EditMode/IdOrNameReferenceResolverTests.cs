using System.Collections.Generic;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class IdOrNameReferenceResolverTests
    {
        SourceTable _quetes;
        SourceTable _puzzles;
        TableMapping _quetesMapping;
        TableMapping _puzzlesMapping;
        Dictionary<string, SourceTable> _sourceTables;
        Dictionary<string, TableMapping> _tableMappings;
        IdOrNameReferenceResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            // Mirrors the real Cryptique dataset: Puzzles."Quête liée" references a Quetes row by
            // its Name, not its ID — the exact mixed-key situation the resolver must handle.
            _quetes = new SourceTable("Quetes", new List<string> { "ID", "Nom" });
            _quetes.AddRow(new Dictionary<string, string> { ["ID"] = "Q_001", ["Nom"] = "Rencontrer Tsuki" });
            _quetes.AddRow(new Dictionary<string, string> { ["ID"] = "Q_004", ["Nom"] = "Convaincre le cheval" });

            _puzzles = new SourceTable("Puzzles", new List<string> { "ID", "Nom", "Quête liée" });
            _puzzles.AddRow(new Dictionary<string, string> { ["ID"] = "PZ_000", ["Nom"] = "jeu de dé", ["Quête liée"] = "Rencontrer Tsuki" });
            _puzzles.AddRow(new Dictionary<string, string> { ["ID"] = "PZ_009", ["Nom"] = "sans quete", ["Quête liée"] = "" });

            _quetesMapping = new TableMapping("Quetes", "ID", TableRole.Quest,
                new List<FieldMapping>(), new List<string>(), new List<ReferenceMapping>());

            var reference = new ReferenceMapping("quest", "Quête liée", new List<string> { "Quetes" },
                new List<ReferenceMatchKey> { ReferenceMatchKey.Id, ReferenceMatchKey.Name("Nom") });

            _puzzlesMapping = new TableMapping("Puzzles", "ID", TableRole.Step,
                new List<FieldMapping>(), new List<string>(), new List<ReferenceMapping> { reference });

            _sourceTables = new Dictionary<string, SourceTable> { ["Quetes"] = _quetes, ["Puzzles"] = _puzzles };
            _tableMappings = new Dictionary<string, TableMapping> { ["Quetes"] = _quetesMapping, ["Puzzles"] = _puzzlesMapping };
            _resolver = new IdOrNameReferenceResolver();
        }

        ReferenceMapping Reference => _puzzlesMapping.References[0];

        [Test]
        public void Resolve_ByName_ReturnsCanonicalId()
        {
            var row = _puzzles.Rows[0]; // "Quête liée" = "Rencontrer Tsuki" (a Name, not an ID)

            var result = _resolver.Resolve(row, Reference, _sourceTables, _tableMappings);

            Assert.IsNotNull(result);
            Assert.AreEqual("Quetes", result.TargetTable);
            Assert.AreEqual("Q_001", result.TargetId);
        }

        [Test]
        public void Resolve_ById_ReturnsCanonicalId()
        {
            var row = _puzzles.AddRow(new Dictionary<string, string> { ["ID"] = "PZ_010", ["Nom"] = "x", ["Quête liée"] = "Q_004" });

            var result = _resolver.Resolve(row, Reference, _sourceTables, _tableMappings);

            Assert.AreEqual("Q_004", result.TargetId);
        }

        [Test]
        public void Resolve_EmptyReferenceCell_ReturnsNull()
        {
            var row = _puzzles.Rows[1]; // "Quête liée" = ""

            var result = _resolver.Resolve(row, Reference, _sourceTables, _tableMappings);

            Assert.IsNull(result);
        }

        [Test]
        public void Resolve_UnknownValue_ThrowsNotFound()
        {
            var row = _puzzles.AddRow(new Dictionary<string, string> { ["ID"] = "PZ_011", ["Nom"] = "x", ["Quête liée"] = "Quête Inexistante" });

            var ex = Assert.Throws<ReferenceResolutionException>(() => _resolver.Resolve(row, Reference, _sourceTables, _tableMappings));
            Assert.AreEqual(ReferenceResolutionReason.NotFound, ex.Reason);
        }

        [Test]
        public void Resolve_AmbiguousValue_ThrowsAmbiguous()
        {
            _quetes.AddRow(new Dictionary<string, string> { ["ID"] = "Q_099", ["Nom"] = "Rencontrer Tsuki" }); // duplicate Name
            var row = _puzzles.Rows[0]; // "Quête liée" = "Rencontrer Tsuki", now matches two rows

            var ex = Assert.Throws<ReferenceResolutionException>(() => _resolver.Resolve(row, Reference, _sourceTables, _tableMappings));
            Assert.AreEqual(ReferenceResolutionReason.Ambiguous, ex.Reason);
        }

        [Test]
        public void Resolve_MatchesBothIdAndName_IsNotAmbiguous()
        {
            // A row whose "Quête liée" value happens to equal Q_001's ID; Q_001's Name is different,
            // so only the ID path matches — must resolve cleanly, not be treated as two candidates.
            var row = _puzzles.AddRow(new Dictionary<string, string> { ["ID"] = "PZ_012", ["Nom"] = "x", ["Quête liée"] = "Q_001" });

            var result = _resolver.Resolve(row, Reference, _sourceTables, _tableMappings);

            Assert.AreEqual("Q_001", result.TargetId);
        }
    }
}
