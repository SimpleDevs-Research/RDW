using UnityEditor;
using UnityEngine;

namespace StreetSim {
    [CustomEditor(typeof(RouteApproximator))]
    public class RouteApproximatorEditor : Editor
    {
        private RouteApproximator ra;

        private void OnEnable() {
            ra = (RouteApproximator)target;
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            if (GUILayout.Button("Approximate")) ra.Approximate();
        }
    }
}

