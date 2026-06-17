using UnityEditor;
using UnityEngine;

namespace StreetSim {
    [CustomEditor(typeof(PedestrianManager))]
    public class PedestrianManagerEditor : Editor
    {
        private PedestrianManager pm;

        private void OnEnable() {
            pm = (PedestrianManager)target;
        }

        public override void OnInspectorGUI() {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(pm.Description, MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}

