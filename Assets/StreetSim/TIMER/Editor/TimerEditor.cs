using UnityEditor;
using UnityEngine;

namespace StreetSim {
    [CustomEditor(typeof(Timer))]
    public class TimerEditor : Editor
    {
        Timer timer;

        private void OnEnable() {
            timer = (Timer)target;
        }
        public override void OnInspectorGUI()
        {
            // Draw the default inspector fields
            DrawDefaultInspector();

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope()) {

                using (new EditorGUI.DisabledScope(!timer.isStopped)) {
                    if (GUILayout.Button("Start", GUILayout.Height(30)))        timer.StartTimer();
                }
                using (new EditorGUI.DisabledScope(timer.isStopped || timer.isRunning)) {
                    if (GUILayout.Button("Play", GUILayout.Height(30)))         timer.PlayTimer();
                }
                using (new EditorGUI.DisabledScope(timer.isStopped || timer.isPaused)) {
                    if (GUILayout.Button("Pause", GUILayout.Height(30)))        timer.PauseTimer();
                }
                using (new EditorGUI.DisabledScope(timer.isStopped)) {
                    if (GUILayout.Button("Stop", GUILayout.Height(30)))         timer.StopTimer();
                }
            }
        }
    }
}
