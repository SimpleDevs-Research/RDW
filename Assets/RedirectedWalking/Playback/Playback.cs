using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class Playback : MonoBehaviour
    {

        public static Playback Instance;

        [Header("=== References ===")]
        public Transform playSpaceAvatar;
        public Transform environmentAvatar;
        public Transform playSpace;
        private Boundary boundary;
        private Environment environment;

        [Header("=== Data Loaded ===")]
        public TextAsset json = null;
        private Session session = null;
        private TextAsset previousJson = null;

        [Header("=== Controls ===")]
        private bool playing = false;
        private float time = 0f;
        private float speed = 1f;
        private float duration;
        public bool repositionBoundary = false;
        public bool orientEnvironment = false; 

        private void OnDrawGizmos() {
            if (boundary == null) return;

            Vector3 boundaryCenter = boundary.transform.position + new Vector3(
                0f, 
                boundary.transform.localScale.y / 2f,
                0f
            );

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(boundaryCenter, boundary.transform.localScale);
        }

        private void Awake() {
            Instance = this;
        }

        private void Start() {
            InitializePlayback();
        }

        private bool TryReadSession(TextAsset t, out Session s) {
            try {
                s = JsonUtility.FromJson<Session>(t.text);
                return true;
            } catch (Exception e) {
                Debug.LogError(e);
                s = null;
                return false;
            }
        } 

        public void InitializePlayback() {
            // Store a reference to the previous json if possible.
            previousJson = json;
            // Can't do anything more if `json` is null or if we can't read the session data
            if (json == null) return;
            if (!TryReadSession(json, out session)) return;
            // At this point, it's safe to initialize the session.
            // Additive Scene Manager will call `InitializeSession` upon loading the intended scene.
            AdditiveSceneManager.Instance.LoadScene(session.sceneName);
        }

        // This is called by AdditiveSceneManager
        public void InitializeSession() {
            // Derive the maximum timestamp, which is technically the duration. Used in OnGUI
            duration = session.duration;
            // Initialize boundary and environment, if possible
            InitializeBoundary();
            InitializeEnvironment();
        }

        public void InitializeBoundary() {
            // If the boundary exists, adjust the boundary to match the dimensions of the recorded space
            if (Boundary.Instance != null) {
                boundary = Boundary.Instance;
                boundary.SetPlayer(playSpaceAvatar);
                boundary.transform.localScale = new Vector3(session.playSpaceSize.x, 3f, session.playSpaceSize.y);
                if (repositionBoundary) playSpace.localPosition = session.worldCenter;
            }
        }

        public void InitializeEnvironment() {
            // If environment exists, make sure it's positioned properly
            if (Environment.Current != null) {
                environment = Environment.Current;
                environmentAvatar.parent = environment.envRoot;
                if (orientEnvironment) environment.transform.position = session.worldCenter;
                environment.StartEnvironment();
            }
        }

        public float GetTimeBounds(Session s, out float min_timestamp, out float max_timestamp) {
            // Minimum timestamp is easy enough; just get the first timestamp ;P
            min_timestamp = s.sessionData[0].timestamp;
            // Max timestamp requires a loop.
            max_timestamp = 0f;
            foreach(State st in s.sessionData) {
                float timestamp = st.timestamp;
                max_timestamp = Mathf.Max(max_timestamp, timestamp);
            }
            // Duration is returned
            return max_timestamp - min_timestamp;
        }

        private void Update() {
            // If we're not playing or if our instance is not set, then end early
            if (!playing || session == null) return;
            // Adjust the current timestamp based on delta time
            time += Time.deltaTime * speed;
            // Loop back to 0 if we reach the end
            if (time > duration) time = 0f;

            // if Playing, proceed
            if (playing) UpdatePlayback();
        }

        private void UpdatePlayback() {
            // `time` is relative to `duration`. So the actual time is `time` + `min_time`
            float t = time;
            // Get the current timestamp index
            if (!TryGetTimestampIndex(t, out int index)) {
                Debug.Log("Could not get timestamp index!");
                return;
            }

            // Get the desired and next state
            var desired_state = session.sessionData[index];
            var next_state = session.sessionData[index + 1];

            // Lerp between the desired state and the next state
            float u = Mathf.InverseLerp(desired_state.timestamp, next_state.timestamp, t);

            // Adjust the position and rotation of the world player
            playSpaceAvatar.localPosition = Vector3.Lerp(desired_state.playerPlaySpacePosition, next_state.playerPlaySpacePosition, u);
            playSpaceAvatar.localRotation = Quaternion.Lerp(desired_state.playerPlaySpaceRotation, next_state.playerPlaySpaceRotation, u);

            // Adjust the position and rotation of the world and env-relative players
            environmentAvatar.localPosition = Vector3.Lerp(desired_state.playerEnvPosition, next_state.playerEnvPosition, u);
            environmentAvatar.localRotation = Quaternion.Lerp(desired_state.playerEnvRotation, next_state.playerEnvRotation, u);

            // Reorient the environment
            if (orientEnvironment) {
                environment.transform.position = Vector3.Lerp(desired_state.envPosition, next_state.envPosition, u);
                environment.transform.rotation = Quaternion.Lerp(desired_state.envRotation, next_state.envRotation, u);
            }
        }

        public virtual bool TryGetTimestampIndex(float t, out int index) {
            // Default: set index = 0
            index = 0;
            
            // Get all state data, terminate early if we don't have any state dta
            var sessionData = session.sessionData;
            if (sessionData == null || sessionData.Count == 0) {
                Debug.LogError("State data is null");
                return false;
            }

            // Handle before first sample
            if (t <= sessionData[0].timestamp) {
                Debug.LogError("Prior to the first timestamp");
                return false;
            }
            // Handle after last sample
            if (t >= sessionData[^1].timestamp) {
                Debug.LogError("After the last timestamp");
                index = sessionData.Count - 1;
                return false;   
            }

            // We're now somewhere between this trajectory's first and last timestamp
            // So now we must interpolate and find the index where the timestamp fits between
            for (int i = 0; i < sessionData.Count - 1; i++) {
                // Grab the current and the next trajectory point
                var a = sessionData[i];
                var b = sessionData[i + 1];
                // Check
                if (t >= a.timestamp && t <= b.timestamp) {
                    index = i;
                    return true;
                }
            }
            // In the worst case, just return false
            Debug.LogError("Worst case - no timestamp found");
            return false;
        }

        private void OnGUI() {
            PlaybackGUI();
        }

        private void PlaybackGUI() {
            GUILayout.BeginArea(new Rect(10, 10, 300, 150), "Playback", GUI.skin.window);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUI.enabled = !playing;
            if (GUILayout.Button("Play")) playing = true;
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUI.enabled = playing;
            if (GUILayout.Button("Pause")) playing = false;
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Label("Time");
            time = GUILayout.HorizontalSlider(time, 0f, duration);

            GUILayout.Label($"Speed: {speed:0.1f}");
            speed = GUILayout.HorizontalSlider(speed, 0.1f, 5f);

            GUILayout.EndArea();
        }

        private void OnValidate() {
            // We use this primarily to check if the use has changed the loaded playback data.
            if (!Application.isPlaying) return;
            if (previousJson != json) {
                // Change detected. We need to:
                // 1. Unparent the avatar for the environment
                environmentAvatar.parent = this.transform;
                // 2. Unset reference to the environment
                environment = null;
                // Query AdditiveSceneManager to switch scenes
                if (previousJson != null && session != null) {
                    AdditiveSceneManager.Instance.UnloadScene(session.sceneName);
                    // Additive Scene Manager should hook to `InitializeSession` upon scene unload.
                }
            }

        }

    }
}
