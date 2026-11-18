using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    [CustomPropertyDrawer(typeof(OccasionIdAttribute))]
    internal sealed class OccasionIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var ids = CatalogDatabase.GetOccasionIds().OrderBy(id => id).ToList();
            var current = property.stringValue;
            var options = new[] { "(none)" }.Concat(ids).ToList();
            var index = string.IsNullOrEmpty(current) ? 0 : ids.IndexOf(current) + 1;

            if (index == 0 && !string.IsNullOrEmpty(current))
            {
                options.Add($"Missing: {current}");
                index = options.Count - 1;
            }

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var next = EditorGUI.Popup(position, label.text, index, options.ToArray());

                if (next != index)
                    property.stringValue = next == 0 ? "" : next <= ids.Count ? ids[next - 1] : current;
            }
        }
    }
}
