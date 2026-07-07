namespace Faolline.GraphCore
{
    /// <summary>
    /// Implemented by <c>ScriptableObject</c> types whose identity is a GUID assigned once (typically in
    /// <c>OnEnable</c>, only when empty) and never editable afterwards — <see cref="BaseGraph"/>,
    /// <see cref="CollectionEntry"/>, <see cref="CollectionDef"/>. Duplicating such an asset (Ctrl+D, or a
    /// file copy outside the editor) copies the serialized id field, so two assets can silently share an
    /// identity that other systems (cycle detection, context collection keys, save data) assume is unique.
    /// <para>
    /// Implementing this interface makes a type discoverable by the editor's stable-id duplicate detector
    /// (<c>StableIdDuplicateDetector</c> in graphcore's Editor assembly), which scans on asset import and
    /// regenerates a duplicate's id automatically — with NO per-type code needed in the detector itself.
    /// Both members are typically implemented EXPLICITLY (<c>string IStableGuidIdentity.StableId => …</c>)
    /// so they stay out of the type's normal public surface (which already exposes the id under its own
    /// name, e.g. <c>GraphId</c> or <c>Key</c>).
    /// </para>
    /// </summary>
    public interface IStableGuidIdentity
    {
        /// <summary>The current stable id. Never null/empty once the asset has been enabled at least once.</summary>
        string StableId { get; }

        /// <summary>
        /// The exact name of the private serialized field backing <see cref="StableId"/> (e.g.
        /// <c>nameof(_graphId)</c>) — read via <c>SerializedObject.FindProperty</c> by editor tooling that
        /// needs to overwrite it (regenerating a duplicate). Never changes at runtime; safe to treat as a
        /// compile-time constant per type.
        /// </summary>
        string StableIdFieldName { get; }
    }
}
