using System.Collections.Generic;

namespace Faolline.GraphImport
{
    /// <summary>Resolves one row's declared reference to a canonical target row identity.</summary>
    public interface IReferenceResolver
    {
        /// <summary>
        /// Returns null when the source cell is empty (no reference declared for this row — not every
        /// quest triggers another one). Throws <see cref="ReferenceResolutionException"/> when the cell
        /// has a value but it resolves to zero or more than one row — never guesses.
        /// </summary>
        PivotReference Resolve(SourceRow fromRow, ReferenceMapping reference,
            IReadOnlyDictionary<string, SourceTable> sourceTables,
            IReadOnlyDictionary<string, TableMapping> tableMappingsByName);
    }
}
