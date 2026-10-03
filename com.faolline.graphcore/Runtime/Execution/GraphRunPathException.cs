using System;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Thrown by <see cref="BaseRunner.StartFromPath"/> when the execution path does not match the graphs it walks.
    /// Thrown before the runner or the context is modified. Check first with
    /// <see cref="GraphRunPath.TryResolve(BaseGraph, System.Collections.Generic.IReadOnlyList{GraphRunFrame}, out GraphRunPathError)"/>
    /// to handle a mismatch without an exception.
    /// </summary>
    public sealed class GraphRunPathException : InvalidOperationException
    {
        /// <summary>The mismatch that was found.</summary>
        public GraphRunPathError Error { get; }

        /// <summary>Creates the exception for <paramref name="error"/>.</summary>
        public GraphRunPathException(GraphRunPathError error)
            : base($"[GraphCore] Cannot restore the execution path: {error?.Message}")
        {
            Error = error;
        }
    }
}
