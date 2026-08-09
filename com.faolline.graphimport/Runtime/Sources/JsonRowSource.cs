using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Faolline.GraphImport
{
    /// <summary>Reads an array-of-objects JSON file into a <see cref="SourceTable"/>.</summary>
    public sealed class JsonRowSource : IRowSource
    {
        public SourceTable Read(string filePath, string tableName)
        {
            var array = JArray.Parse(File.ReadAllText(filePath));

            var header = new List<string>();
            var seen = new HashSet<string>();
            foreach (var element in array.OfType<JObject>())
            {
                foreach (var property in element.Properties())
                {
                    if (seen.Add(property.Name))
                        header.Add(property.Name);
                }
            }

            var table = new SourceTable(tableName, header);

            foreach (var element in array.OfType<JObject>())
            {
                var values = new Dictionary<string, string>();
                foreach (var property in element.Properties())
                    values[property.Name] = property.Value.Type == JTokenType.Null
                        ? string.Empty
                        : property.Value.ToString();
                table.AddRow(values);
            }

            return table;
        }
    }
}
