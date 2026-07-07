using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A single reference from an action/condition to a <see cref="ParameterName"/> asset, tagged with the
    /// <see cref="ParameterType"/> that reference site expects. The scanner reads <see cref="Parameter"/> for
    /// seeding; the validator compares <see cref="ExpectedType"/> against <c>Parameter.Type</c> to catch a
    /// type-mismatched wiring (e.g. a <c>SetIntAction</c> pointed at a <c>Float</c> parameter).
    /// </summary>
    public readonly struct ParameterReference
    {
        /// <summary>The referenced parameter asset (may be <c>null</c> for an unassigned field).</summary>
        public readonly ParameterName Parameter;

        /// <summary>The type this reference site requires the parameter to be.</summary>
        public readonly ParameterType ExpectedType;

        public ParameterReference(ParameterName parameter, ParameterType expectedType)
        {
            Parameter = parameter;
            ExpectedType = expectedType;
        }
    }

    /// <summary>
    /// Implemented by an action or condition that references one or more <see cref="ParameterName"/> assets.
    /// This is the opt-in contract that makes parameters <b>declaration-free</b>: instead of a per-graph
    /// parameter list, <see cref="BaseContext.InitFromGraph"/> walks a graph's action/condition sites, collects
    /// every referenced <see cref="ParameterName"/> via this interface, and seeds each one's default onto the
    /// context. The graph validator also reads it to type-check each reference, and <c>GraphParams</c> generation
    /// scans the project's assets (not graphs) for the same identity model.
    /// <para>
    /// A custom action that does not implement this simply leaves its parameters unseeded — the host is then
    /// responsible for setting them (e.g. through <c>GraphParams</c> constants). That is safe; it only forgoes
    /// the auto-seed convenience.
    /// </para>
    /// </summary>
    public interface IParameterReferencing
    {
        /// <summary>
        /// The <see cref="ParameterReference"/>s this action/condition reads or writes, each tagged with the type
        /// it expects. May be empty; a reference's <see cref="ParameterReference.Parameter"/> may be <c>null</c>
        /// (an unassigned field) and is skipped by callers. Never returns <c>null</c> itself.
        /// </summary>
        IEnumerable<ParameterReference> ReferencedParameters { get; }
    }
}
