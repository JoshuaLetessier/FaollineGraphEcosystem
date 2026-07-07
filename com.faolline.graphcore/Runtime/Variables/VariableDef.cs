using System;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A typed, named parameter as a reusable asset — drag-drop onto an action/condition instead of typing a
    /// string key. Its identity is a stable GUID (<see cref="Key"/>), assigned once in <c>OnEnable</c> and never
    /// editable — the same model as <see cref="SignalDef"/>/<see cref="CollectionEntry"/> and
    /// <see cref="BaseGraph.GraphId"/>. That GUID is the key actually written/read on the context and stored in a
    /// save; renaming the asset file, or the display <see cref="DisplayName"/>, never changes it.
    /// <para>
    /// Unlike <see cref="SignalDef"/>, a parameter also carries a <see cref="VariableType"/> and a typed
    /// <see cref="DefaultValueBoxed">default</see>. The asset IS the declaration: there is no per-graph parameter
    /// list — <see cref="BaseContext.InitFromGraph"/> discovers every <see cref="VariableDef"/> referenced by a
    /// graph's actions/conditions (via <see cref="IVariableReferencing"/>) and seeds each one's default.
    /// </para>
    /// <para>
    /// <see cref="DisplayName"/> is a purely cosmetic label (editor tooling, and the seed for the generated
    /// <c>GraphVariables</c> constants) — NEVER the runtime key, so it can be renamed freely: the data
    /// (sets/gets/saves) keeps matching on the unchanged GUID, and only the regenerated code constant's symbol
    /// changes (breaking stale code at compile — the intended, safe rename).
    /// </para>
    /// <para>
    /// <b>Islands:</b> asset-based parameters key on the GUID; the raw-string channel
    /// (<see cref="BaseContext.Set{T}(string,T)"/> with a literal) keys on literals. The two do not cross — a raw
    /// <c>Set&lt;int&gt;("hp", …)</c> does not feed a condition reading THIS asset. To reference an asset
    /// parameter from code, use the generated constant (its GUID) or a held <see cref="VariableDef"/> reference.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Variable Name", fileName = "NewParameter")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_action.png")]
    public class VariableDef : ScriptableObject, IStableGuidIdentity
    {
        [SerializeField, HideInInspector] private string _id;

        [SerializeField, Tooltip("Cosmetic display label for editor tooling and the seed for the generated " +
            "GraphVariables constant symbol. NEVER the runtime key — rename it freely; the parameter's identity is " +
            "its stable GUID. Falls back to the asset name when empty.")]
        private string _name;

        [SerializeField, Tooltip("The data type of this parameter. Actions/conditions that reference it must " +
            "match this type (enforced by the graph validator).")]
        private VariableType _type;

        // Typed defaults — only the one matching _type is meaningful. Seeded into a context by InitFromGraph.
        [SerializeField] private bool    _boolDefault;
        [SerializeField] private int     _intDefault;
        [SerializeField] private float   _floatDefault;
        [SerializeField] private string  _stringDefault = string.Empty;
        [SerializeField] private Vector2 _vector2Default;
        [SerializeField] private Vector3 _vector3Default;
        [SerializeField] private Color   _colorDefault = new Color(1f, 1f, 1f, 1f);

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_id))
            {
                _id = Guid.NewGuid().ToString("D");
#if UNITY_EDITOR
                StableGuidPersistence.ScheduleSave(this);   // persist the assignment — see StableGuidPersistence
#endif
            }
        }

        /// <summary>
        /// Stable GUID identity — the string actually written/read/matched/saved as the context key. Assigned
        /// once in <c>OnEnable</c>, never editable, independent of the asset file name and the display name.
        /// </summary>
        public string Key => _id;

        /// <summary>Human-readable display label (editor tooling / codegen seed). Falls back to the asset name when empty. Never the runtime key.</summary>
        public string DisplayName => string.IsNullOrEmpty(_name) ? name : _name;

        /// <summary>The data type of this parameter. Referencing actions/conditions must match it.</summary>
        public VariableType Type => _type;

        // Explicit IStableGuidIdentity: discoverable by StableIdDuplicateDetector with no per-type code there.
        string IStableGuidIdentity.StableId => _id;
        string IStableGuidIdentity.StableIdFieldName => nameof(_id);

        /// <summary>The default value for <see cref="Type"/>, boxed — what a context seed writes. Never throws.</summary>
        public object DefaultValueBoxed
        {
            get
            {
                switch (_type)
                {
                    case VariableType.Bool:    return _boolDefault;
                    case VariableType.Int:     return _intDefault;
                    case VariableType.Float:   return _floatDefault;
                    case VariableType.String:  return _stringDefault ?? string.Empty;
                    case VariableType.Vector2: return _vector2Default;
                    case VariableType.Vector3: return _vector3Default;
                    case VariableType.Color:   return _colorDefault;
                    default:                    return null;
                }
            }
        }

        /// <summary>The runtime key is the GUID — so an asset-based set/get keys on the stable identity.</summary>
        public static implicit operator string(VariableDef parameter)
            => parameter != null ? parameter.Key : string.Empty;

        // ── Factories (code-first authoring / tests) ────────────────────────────
        // Each mints a runtime VariableDef with a fresh GUID identity, the given display label, its type, and
        // its typed default. The identity is the fresh GUID (Key), NOT the display name; two calls with the same
        // label produce two distinct parameters. Mirrors SignalDef.Create + the old ParameterData factories.

        private static VariableDef New(string displayName, VariableType type)
        {
            var p = CreateInstance<VariableDef>();   // OnEnable assigns _id
            p._name = displayName;
            p._type = type;
            return p;
        }

        /// <summary>A bool parameter with the given display name and default.</summary>
        public static VariableDef Bool(string displayName, bool value = false)
        { var p = New(displayName, VariableType.Bool); p._boolDefault = value; return p; }

        /// <summary>An int parameter with the given display name and default.</summary>
        public static VariableDef Int(string displayName, int value = 0)
        { var p = New(displayName, VariableType.Int); p._intDefault = value; return p; }

        /// <summary>A float parameter with the given display name and default.</summary>
        public static VariableDef Float(string displayName, float value = 0f)
        { var p = New(displayName, VariableType.Float); p._floatDefault = value; return p; }

        /// <summary>A string parameter with the given display name and default.</summary>
        public static VariableDef String(string displayName, string value = "")
        { var p = New(displayName, VariableType.String); p._stringDefault = value ?? string.Empty; return p; }

        /// <summary>A Vector2 parameter with the given display name and default.</summary>
        public static VariableDef Vector2(string displayName, Vector2 value = default)
        { var p = New(displayName, VariableType.Vector2); p._vector2Default = value; return p; }

        /// <summary>A Vector3 parameter with the given display name and default.</summary>
        public static VariableDef Vector3(string displayName, Vector3 value = default)
        { var p = New(displayName, VariableType.Vector3); p._vector3Default = value; return p; }

        /// <summary>A Color parameter with the given display name and default.</summary>
        public static VariableDef Color(string displayName, Color value = default)
        { var p = New(displayName, VariableType.Color); p._colorDefault = value; return p; }
    }
}
