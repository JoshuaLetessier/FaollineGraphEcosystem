using System;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A named signal as a reusable asset — drag-drop instead of typing a string. Its identity is a stable
    /// GUID (<see cref="Key"/>), assigned once in <c>OnEnable</c> and never editable — the same model as
    /// <see cref="CollectionEntry"/>/<see cref="CollectionName"/> and <see cref="BaseGraph.GraphId"/>. That
    /// GUID is what is raised into the context, awaited, matched, and stored in a save; renaming the asset
    /// file, or the display <see cref="DisplayName"/>, never changes it. Duplicating the asset (Ctrl+D) yields
    /// a fresh GUID via the stable-id duplicate detector.
    /// <para>
    /// <see cref="DisplayName"/> is a purely cosmetic label (editor tooling, and the seed for the generated
    /// <c>GraphSignals</c> constants) — it is NEVER the runtime key, so it can be renamed freely: the data
    /// (awaits/raises/saves) keeps matching on the unchanged GUID, and only the regenerated code constant's
    /// symbol changes (breaking stale code at compile — the intended, safe rename).
    /// </para>
    /// <para>
    /// <b>Islands:</b> asset-based signals key on the GUID; the raw-string channel
    /// (<see cref="BaseRunner.RaiseSignal(string)"/> with a literal, and <see cref="BaseNodeData.AwaitSignalName"/>
    /// as a raw field) keys on literals. The two do not cross — a raw <c>RaiseSignal("advance")</c> does not
    /// wake a node awaiting THIS asset. To raise an asset signal from code, use the generated constant (its
    /// GUID) or a held <see cref="SignalName"/> reference.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Signal Name", fileName = "NewSignal")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_signal.png")]
    public class SignalName : ScriptableObject, IStableGuidIdentity
    {
        [SerializeField, HideInInspector] private string _id;

        [SerializeField, Tooltip("Cosmetic display label for editor tooling and the seed for the generated " +
            "GraphSignals constant symbol. NEVER the runtime key — rename it freely; the signal's identity is " +
            "its stable GUID. Falls back to the asset name when empty.")]
        private string _name;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_id))
                _id = Guid.NewGuid().ToString("D");
        }

        /// <summary>
        /// Stable GUID identity — the string actually raised/awaited/matched/saved. Assigned once in
        /// <c>OnEnable</c>, never editable, independent of the asset file name and the display name.
        /// </summary>
        public string Key => _id;

        /// <summary>Human-readable display label (editor tooling / codegen seed). Falls back to the asset name when empty. Never the runtime key.</summary>
        public string DisplayName => string.IsNullOrEmpty(_name) ? name : _name;

        // Explicit IStableGuidIdentity: discoverable by StableIdDuplicateDetector with no per-type code there.
        string IStableGuidIdentity.StableId => _id;
        string IStableGuidIdentity.StableIdFieldName => nameof(_id);

        /// <summary>The runtime key is the GUID — so an asset-based raise/await keys on the stable identity.</summary>
        public static implicit operator string(SignalName signal)
            => signal != null ? signal.Key : string.Empty;

        /// <summary>
        /// Creates a runtime <see cref="SignalName"/> instance with a fresh GUID identity and the given
        /// display label — for code that builds signals dynamically, and for tests. The identity is the
        /// fresh GUID (<see cref="Key"/>), NOT <paramref name="displayName"/>; two calls with the same
        /// label produce two distinct signals.
        /// </summary>
        public static SignalName Create(string displayName)
        {
            var s = CreateInstance<SignalName>();   // OnEnable assigns _id
            s._name = displayName;
            return s;
        }
    }
}
