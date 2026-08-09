using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Faolline.GraphImport
{
    /// <summary>Which pivot entity kind a source table's rows become.</summary>
    public enum TableRole
    {
        Quest,
        Step,
        Content,
        Lookup
    }

    /// <summary>Declarative, per-project mapping: which columns matter, and how references resolve.</summary>
    public sealed class MappingConfig
    {
        public IReadOnlyList<TableMapping> Tables { get; }

        public MappingConfig(IReadOnlyList<TableMapping> tables)
        {
            Tables = tables;
        }

        public IReadOnlyDictionary<string, TableMapping> TableMappingsByName =>
            Tables.ToDictionary(t => t.SourceTableName);

        public static MappingConfig LoadFromJson(string json)
        {
            var root = JObject.Parse(json);
            var tables = new List<TableMapping>();

            foreach (var tableToken in (JArray)root["tables"])
            {
                var t = (JObject)tableToken;

                var fields = (t["fields"] as JArray)?.Select(f => new FieldMapping(
                    (string)f["pivotField"],
                    (string)f["sourceColumn"]
                )).ToList() ?? new List<FieldMapping>();

                var ignore = (t["ignore"] as JArray)?.Select(v => (string)v).ToList() ?? new List<string>();

                var references = (t["references"] as JArray)?.Select(r => new ReferenceMapping(
                    (string)r["pivotField"],
                    (string)r["sourceColumn"],
                    ((JArray)r["targetTables"]).Select(v => (string)v).ToList(),
                    ((JArray)r["matchOn"]).Select(ParseMatchKey).ToList()
                )).ToList() ?? new List<ReferenceMapping>();

                tables.Add(new TableMapping(
                    (string)t["sourceTableName"],
                    (string)t["idColumn"],
                    ParseRole((string)t["role"]),
                    fields,
                    ignore,
                    references
                ));
            }

            return new MappingConfig(tables);
        }

        static ReferenceMatchKey ParseMatchKey(JToken token)
        {
            if (token.Type == JTokenType.String && (string)token == "Id")
                return ReferenceMatchKey.Id;
            if (token is JObject obj && obj["nameColumn"] != null)
                return ReferenceMatchKey.Name((string)obj["nameColumn"]);
            throw new MappingValidationException(new[] { $"Unrecognized matchOn entry: {token}" });
        }

        static TableRole ParseRole(string value)
        {
            if (Enum.TryParse<TableRole>(value, ignoreCase: true, out var role))
                return role;
            throw new MappingValidationException(new[] { $"Unknown table role '{value}' — expected one of: {string.Join(", ", Enum.GetNames(typeof(TableRole)))}" });
        }

        /// <summary>
        /// Checks every declared column against the actual source tables. Collects every problem
        /// found (not just the first) so a typo'd mapping fails loud and in full, before any pivot
        /// building is attempted.
        /// </summary>
        public void Validate(IReadOnlyDictionary<string, SourceTable> sourceTables)
        {
            var errors = new List<string>();

            foreach (var table in Tables)
            {
                if (!sourceTables.TryGetValue(table.SourceTableName, out var sourceTable))
                {
                    errors.Add($"Mapping declares table '{table.SourceTableName}' which was not provided.");
                    continue;
                }

                var header = new HashSet<string>(sourceTable.Header);

                if (!header.Contains(table.IdColumn))
                    errors.Add($"Table '{table.SourceTableName}': idColumn '{table.IdColumn}' is not a column of the source table.");

                foreach (var field in table.Fields)
                    if (!header.Contains(field.SourceColumn))
                        errors.Add($"Table '{table.SourceTableName}': field '{field.PivotField}' declares sourceColumn '{field.SourceColumn}' which is not a column of the source table.");

                foreach (var reference in table.References)
                {
                    if (!header.Contains(reference.SourceColumn))
                        errors.Add($"Table '{table.SourceTableName}': reference '{reference.PivotField}' declares sourceColumn '{reference.SourceColumn}' which is not a column of the source table.");

                    foreach (var targetTableName in reference.TargetTables)
                    {
                        if (!sourceTables.TryGetValue(targetTableName, out var targetTable))
                        {
                            errors.Add($"Table '{table.SourceTableName}': reference '{reference.PivotField}' targets table '{targetTableName}' which was not provided.");
                            continue;
                        }

                        var targetHeader = new HashSet<string>(targetTable.Header);
                        foreach (var key in reference.MatchOn)
                        {
                            if (!key.IsId && !targetHeader.Contains(key.NameColumn))
                                errors.Add($"Table '{table.SourceTableName}': reference '{reference.PivotField}' declares matchOn nameColumn '{key.NameColumn}' which is not a column of target table '{targetTableName}'.");
                        }
                    }
                }
            }

            if (errors.Count > 0)
                throw new MappingValidationException(errors);
        }
    }

    public sealed class TableMapping
    {
        public string SourceTableName { get; }
        public string IdColumn { get; }
        public TableRole Role { get; }
        public IReadOnlyList<FieldMapping> Fields { get; }
        public IReadOnlyList<string> Ignore { get; }
        public IReadOnlyList<ReferenceMapping> References { get; }

        public TableMapping(string sourceTableName, string idColumn, TableRole role,
            IReadOnlyList<FieldMapping> fields, IReadOnlyList<string> ignore, IReadOnlyList<ReferenceMapping> references)
        {
            SourceTableName = sourceTableName;
            IdColumn = idColumn;
            Role = role;
            Fields = fields;
            Ignore = ignore;
            References = references;
        }
    }

    public sealed class FieldMapping
    {
        public string PivotField { get; }
        public string SourceColumn { get; }

        public FieldMapping(string pivotField, string sourceColumn)
        {
            PivotField = pivotField;
            SourceColumn = sourceColumn;
        }
    }

    public sealed class ReferenceMapping
    {
        public string PivotField { get; }
        public string SourceColumn { get; }
        public IReadOnlyList<string> TargetTables { get; }
        public IReadOnlyList<ReferenceMatchKey> MatchOn { get; }

        public ReferenceMapping(string pivotField, string sourceColumn, IReadOnlyList<string> targetTables, IReadOnlyList<ReferenceMatchKey> matchOn)
        {
            PivotField = pivotField;
            SourceColumn = sourceColumn;
            TargetTables = targetTables;
            MatchOn = matchOn;
        }
    }

    /// <summary>One resolution strategy to try for a reference: the target's stable ID, or a named fallback column.</summary>
    public readonly struct ReferenceMatchKey
    {
        public bool IsId { get; }
        public string NameColumn { get; }

        ReferenceMatchKey(bool isId, string nameColumn)
        {
            IsId = isId;
            NameColumn = nameColumn;
        }

        public static ReferenceMatchKey Id { get; } = new ReferenceMatchKey(true, null);
        public static ReferenceMatchKey Name(string column) => new ReferenceMatchKey(false, column);
    }

    /// <summary>Raised when a mapping configuration doesn't match the actual source tables. Lists every problem found.</summary>
    public sealed class MappingValidationException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public MappingValidationException(IReadOnlyList<string> errors) : base(string.Join("\n", errors))
        {
            Errors = errors;
        }
    }
}
