using UnityEditor;
using UnityEngine;

namespace StreetSim {
    [CustomEditor(typeof(RouteManager))]
    public class RouteManagerEditor : Editor
    {
        private RouteManager rm;

        private void OnEnable() {
            rm = (RouteManager)target;
        }

        public override void OnInspectorGUI() {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(rm.Description, MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}

