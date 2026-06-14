using System;
using System.Globalization;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A typed, named variable scoped to a single <see cref="BaseGraph"/>. The default value is stored in a
    /// field that matches <see cref="Type"/> (no string parsing at run time) — read it boxed via
    /// <see cref="DefaultValueBoxed"/> when seeding a context. Older assets stored the default as a single string
    /// (<c>_defaultValue</c>); that legacy value is migrated into the right typed field lazily on first access.
    /// </summary>
    [Serializable]
    public class ParameterData
    {
        [SerializeField] private string _key;
        [SerializeField] private ParameterType _type;

        // Typed defaults — only the one matching _type is meaningful.
        [SerializeField] private bool    _boolDefault;
        [SerializeField] private int     _intDefault;
        [SerializeField] private float   _floatDefault;
        [SerializeField] private string  _stringDefault = string.Empty;
        [SerializeField] private Vector2 _vector2Default;
        [SerializeField] private Vector3 _vector3Default;
        [SerializeField] private Color   _colorDefault = new Color(1f, 1f, 1f, 1f);

        // Legacy pre-typed default. Migrated into the typed field on first access, then cleared. Hidden — kept
        // only so existing assets self-upgrade; it is re-saved away on the next write.
        [SerializeField, HideInInspector] private string _defaultValue;

        /// <summary>Variable name. Uniqueness is enforced by the runtime, not the data layer.</summary>
        public string Key { get => _key; set => _key = value; }

        /// <summary>The data type of this parameter.</summary>
        public ParameterType Type { get => _type; set => _type = value; }

        /// <summary>The bool default (meaningful when <see cref="Type"/> is <see cref="ParameterType.Bool"/>).</summary>
        public bool BoolDefault { get { MigrateLegacy(); return _boolDefault; } set => _boolDefault = value; }

        /// <summary>The int default (meaningful when <see cref="Type"/> is <see cref="ParameterType.Int"/>).</summary>
        public int IntDefault { get { MigrateLegacy(); return _intDefault; } set => _intDefault = value; }

        /// <summary>The float default (meaningful when <see cref="Type"/> is <see cref="ParameterType.Float"/>).</summary>
        public float FloatDefault { get { MigrateLegacy(); return _floatDefault; } set => _floatDefault = value; }

        /// <summary>The string default (meaningful when <see cref="Type"/> is <see cref="ParameterType.String"/>).</summary>
        public string StringDefault { get { MigrateLegacy(); return _stringDefault ?? string.Empty; } set => _stringDefault = value ?? string.Empty; }

        /// <summary>The Vector2 default (meaningful when <see cref="Type"/> is <see cref="ParameterType.Vector2"/>).</summary>
        public Vector2 Vector2Default { get => _vector2Default; set => _vector2Default = value; }

        /// <summary>The Vector3 default (meaningful when <see cref="Type"/> is <see cref="ParameterType.Vector3"/>).</summary>
        public Vector3 Vector3Default { get => _vector3Default; set => _vector3Default = value; }

        /// <summary>The Color default (meaningful when <see cref="Type"/> is <see cref="ParameterType.Color"/>).</summary>
        public Color ColorDefault { get => _colorDefault; set => _colorDefault = value; }

        /// <summary>The default value for <see cref="Type"/>, boxed — what a context seed writes. Never throws.</summary>
        public object DefaultValueBoxed
        {
            get
            {
                MigrateLegacy();
                switch (_type)
                {
                    case ParameterType.Bool:    return _boolDefault;
                    case ParameterType.Int:     return _intDefault;
                    case ParameterType.Float:   return _floatDefault;
                    case ParameterType.String:  return _stringDefault ?? string.Empty;
                    case ParameterType.Vector2: return _vector2Default;
                    case ParameterType.Vector3: return _vector3Default;
                    case ParameterType.Color:   return _colorDefault;
                    default:                    return null;
                }
            }
        }

        /// <summary>
        /// Legacy accessor: the default as a string. Reading returns the typed value formatted (invariant);
        /// writing stores the string for one-time migration into the matching typed field. Prefer the typed
        /// properties / factory methods.
        /// </summary>
        public string DefaultValue
        {
            get
            {
                MigrateLegacy();
                switch (_type)
                {
                    case ParameterType.Bool:   return _boolDefault ? "true" : "false";
                    case ParameterType.Int:    return _intDefault.ToString(CultureInfo.InvariantCulture);
                    case ParameterType.Float:  return _floatDefault.ToString(CultureInfo.InvariantCulture);
                    default:                   return _stringDefault ?? string.Empty;
                }
            }
            // Store as legacy; migrated into the typed field lazily on the next read (when Type is final, so the
            // value is parsed for the right type regardless of object-initializer field order).
            set => _defaultValue = value;
        }

        // ── Factories ───────────────────────────────────────────────────────────
        /// <summary>A bool parameter.</summary>
        public static ParameterData Bool(string key, bool value = false)
            => new ParameterData { _key = key, _type = ParameterType.Bool, _boolDefault = value };
        /// <summary>An int parameter.</summary>
        public static ParameterData Int(string key, int value = 0)
            => new ParameterData { _key = key, _type = ParameterType.Int, _intDefault = value };
        /// <summary>A float parameter.</summary>
        public static ParameterData Float(string key, float value = 0f)
            => new ParameterData { _key = key, _type = ParameterType.Float, _floatDefault = value };
        /// <summary>A string parameter.</summary>
        public static ParameterData String(string key, string value = "")
            => new ParameterData { _key = key, _type = ParameterType.String, _stringDefault = value ?? string.Empty };

        /// <summary>A Vector2 parameter.</summary>
        public static ParameterData Vector2(string key, Vector2 value = default)
            => new ParameterData { _key = key, _type = ParameterType.Vector2, _vector2Default = value };

        /// <summary>A Vector3 parameter.</summary>
        public static ParameterData Vector3(string key, Vector3 value = default)
            => new ParameterData { _key = key, _type = ParameterType.Vector3, _vector3Default = value };

        /// <summary>A Color parameter.</summary>
        public static ParameterData Color(string key, Color value = default)
            => new ParameterData { _key = key, _type = ParameterType.Color, _colorDefault = value };

        // One-time migration of the legacy string default into the typed field for the current type.
        private void MigrateLegacy()
        {
            if (string.IsNullOrEmpty(_defaultValue)) return;
            var legacy = _defaultValue;
            _defaultValue = null;   // consume first to avoid re-entry via the typed getters below
            switch (_type)
            {
                case ParameterType.Bool:   if (bool.TryParse(legacy, out var b)) _boolDefault = b; break;
                case ParameterType.Int:    if (int.TryParse(legacy, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) _intDefault = i; break;
                case ParameterType.Float:  if (float.TryParse(legacy, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) _floatDefault = f; break;
                case ParameterType.String: _stringDefault = legacy; break;
            }
        }
    }
}
