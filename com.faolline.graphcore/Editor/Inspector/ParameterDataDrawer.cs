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

            var boolField   = new PropertyField(property.FindPropertyRelative("_boolDefault"),   "Default");
            var intField    = new PropertyField(property.FindPropertyRelative("_intDefault"),    "Default");
            var floatField  = new PropertyField(property.FindPropertyRelative("_floatDefault"),  "Default");
            var stringField = new PropertyField(property.FindPropertyRelative("_stringDefault"), "Default");
            root.Add(boolField);
            root.Add(intField);
            root.Add(floatField);
            root.Add(stringField);

            void Refresh()
            {
                var type = (ParameterType)typeProp.enumValueIndex;
                boolField.style.display   = type == ParameterType.Bool   ? DisplayStyle.Flex : DisplayStyle.None;
                intField.style.display    = type == ParameterType.Int    ? DisplayStyle.Flex : DisplayStyle.None;
                floatField.style.display  = type == ParameterType.Float  ? DisplayStyle.Flex : DisplayStyle.None;
                stringField.style.display = type == ParameterType.String ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Refresh();
            typeField.RegisterValueChangeCallback(_ => Refresh());

            return root;
        }
    }
}
