using UnityEditor;
using UnityEngine;

namespace StreetSim {
    [CustomPropertyDrawer(typeof(Route))]
    public class RouteDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            // Get the property for `is_active`
            SerializedProperty activeProp = property.FindPropertyRelative("is_active");

            // Checkbox rect
            Rect toggleRect = new Rect(
                position.x,
                position.y,
                20,
                EditorGUIUtility.singleLineHeight);

            // Foldout rect
            Rect foldoutRect = new Rect(
                position.x + 20,
                position.y,
                position.width - 20,
                EditorGUIUtility.singleLineHeight);

            activeProp.boolValue = EditorGUI.Toggle(toggleRect, activeProp.boolValue);

            property.isExpanded = EditorGUI.Foldout(
                    foldoutRect,
                    property.isExpanded,
                    label,
                    true);

            if (property.isExpanded) {
                Rect contentRect = new Rect(
                    position.x,
                    position.y + EditorGUIUtility.singleLineHeight + 2,
                    position.width,
                    EditorGUI.GetPropertyHeight(property, true));

                EditorGUI.indentLevel++;

                EditorGUI.PropertyField(
                    contentRect,
                    property,
                    GUIContent.none,
                    true);

                EditorGUI.indentLevel--;
            }
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded) {
                height += EditorGUI.GetPropertyHeight(property, true) + 2;
            }
            return height;
        }
    }
}