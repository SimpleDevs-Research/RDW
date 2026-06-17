using UnityEditor;
using UnityEngine;

namespace StreetSim {
    [CustomEditor(typeof(CapsulePrism))]
    public class CapsulePrismEditor : Editor
    {
        private CapsulePrism cp;

        private void OnEnable() {
            cp = (CapsulePrism)target;
        }

        public override void OnInspectorGUI() {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(cp.Description, MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
        
    }
}

