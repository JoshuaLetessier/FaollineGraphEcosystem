using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Draws a <see cref="ParameterData"/> as Key + Type + a SINGLE default field matching the chosen
    /// <see cref="ParameterType"/> (instead of showing all four typed default fields at once). The visible
    /// default field swaps live when the type changes.
    /// </summary>
    [CustomPropertyDrawer(typeof(ParameterData))]
    public sealed class ParameterDataDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            var typeProp = property.FindPropertyRelative("_type");

            root.Add(new PropertyField(property.FindPropertyRelative("_key"), "Key"));
            var typeField = new PropertyField(typeProp, "Type");
            root.Add(typeField);

            var boolField    = new PropertyField(property.FindPropertyRelative("_boolDefault"),    "Default");
            var intField     = new PropertyField(property.FindPropertyRelative("_intDefault"),     "Default");
            var floatField   = new PropertyField(property.FindPropertyRelative("_floatDefault"),   "Default");
            var stringField  = new PropertyField(property.FindPropertyRelative("_stringDefault"),  "Default");
            var vector2Field = new PropertyField(property.FindPropertyRelative("_vector2Default"), "Default");
            var vector3Field = new PropertyField(property.FindPropertyRelative("_vector3Default"), "Default");
            var colorField   = new PropertyField(property.FindPropertyRelative("_colorDefault"),   "Default");
            root.Add(boolField);
            root.Add(intField);
            root.Add(floatField);
            root.Add(stringField);
            root.Add(vector2Field);
            root.Add(vector3Field);
            root.Add(colorField);

            void Refresh()
            {
                var type = (ParameterType)typeProp.enumValueIndex;
                boolField.style.display    = type == ParameterType.Bool    ? DisplayStyle.Flex : DisplayStyle.None;
                intField.style.display     = type == ParameterType.Int     ? DisplayStyle.Flex : DisplayStyle.None;
                floatField.style.display   = type == ParameterType.Float   ? DisplayStyle.Flex : DisplayStyle.None;
                stringField.style.display  = type == ParameterType.String  ? DisplayStyle.Flex : DisplayStyle.None;
                vector2Field.style.display = type == ParameterType.Vector2 ? DisplayStyle.Flex : DisplayStyle.None;
                vector3Field.style.display = type == ParameterType.Vector3 ? DisplayStyle.Flex : DisplayStyle.None;
                colorField.style.display   = type == ParameterType.Color   ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Refresh();
            typeField.RegisterValueChangeCallback(_ => Refresh());

            return root;
        }
    }
}
