using System;
using System.Collections.Generic;

namespace Faolline.GraphImport.Editor
{
    /// <summary>One plan entry whose generator threw while being applied.</summary>
    public sealed class GenerationFailure
    {
        public PlanEntry Entry { get; }
        public Exception Exception { get; }

        public GenerationFailure(PlanEntry entry, Exception exception)
        {
            Entry = entry;
            Exception = exception;
        }
    }

    /// <summary>
    /// The full outcome of an apply run: what was actually created, and what a generator failed to
    /// build. A failure never aborts the rest of the run — every other non-conflicting entry still
    /// gets its chance, so a single bad entry can't hide the fact that everything else succeeded.
    /// </summary>
    public sealed class ApplyResult
    {
        public IReadOnlyList<PlanEntry> Created { get; }
        public IReadOnlyList<GenerationFailure> Failures { get; }
        public bool IsClean => Failures.Count == 0;

        public ApplyResult(IReadOnlyList<PlanEntry> created, IReadOnlyList<GenerationFailure> failures)
        {
            Created = created;
            Failures = failures;
        }
    }
}
