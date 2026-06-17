using UnityEditor;
using UnityEngine;

namespace StreetSim {
    [CustomEditor(typeof(PathRegion))]
    public class PathRegionEditor : Editor
    {
        private PathRegion pr;

        private void OnEnable() {
            pr = (PathRegion)target;
        }

        public override void OnInspectorGUI() {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(pr.Description, MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}

