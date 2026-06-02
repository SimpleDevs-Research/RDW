using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class Playback : MonoBehaviour
    {

        [Header("=== References ===")]
        public Transform world_player_ref;
        public Transform env_player_ref;
        public Transform environment_ref;
        public Transform boundary_ref;

        [Header("=== Data Loaded ===")]
        public TextAsset json = null;
        public Session session = null;

        [Header("=== Controls ===")]
        public bool playing = false;
        public float time = 0f;
        public float speed = 1f;
        public float min_time, max_time, duration;

        private void Start() {
            // Attempt to read the JSON file
            session = JsonUtility.FromJson<Session>(json.text);
            // Derive the maximum timestamp. Used in OnGUI
            duration = GetTimeBounds(session, out min_time, out max_time);
            // If existing, adjust the boundary to match the dimensions of the recorded space
            if (boundary_ref != null) {
                boundary_ref.localPosition = session.world_center_position;
                float width = Mathf.Abs(session.max_anchor_position.x - session.min_anchor_position.x);
                float depth = Mathf.Abs(session.max_anchor_position.z - session.min_anchor_position.z);
                boundary_ref.localScale = new Vector3(width, 1f, depth);
                // Add ref to player if `BoundaryProximity` is a component of boundary ref
                boundary_ref.GetComponent<BoundaryProximity>()?.SetPlayer(world_player_ref);
            }
        }

        public float GetTimeBounds(Session s, out float min_timestamp, out float max_timestamp) {
            // Minimum timestamp is easy enough; just get the first timestamp ;P
            min_timestamp = s.state_data[0].timestamp;
            // Max timestamp requires a loop.
            max_timestamp = 0f;
            foreach(State st in s.state_data) {
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
            float t = min_time + time;
            // Get the current timestamp index
            if (!TryGetTimestampIndex(t, out int index)) {
                Debug.Log("Could not get timestamp index!");
                return;
            }

            // Get the desired and next state
            var desired_state = session.state_data[index];
            var next_state = session.state_data[index + 1];

            // Lerp between the desired state and the next state
            float u = Mathf.InverseLerp(desired_state.timestamp, next_state.timestamp, t);

            // Adjust the position and rotation of the world player
            world_player_ref.localPosition = Vector3.Lerp(desired_state.world_position, next_state.world_position, u);
            world_player_ref.localRotation = Quaternion.Lerp(desired_state.world_rotation, next_state.world_rotation, u);

            // Adjust the position and rotation of the world and env-relative players
            env_player_ref.localPosition = Vector3.Lerp(desired_state.env_position, next_state.env_position, u);
            env_player_ref.localRotation = Quaternion.Lerp(desired_state.env_rotation, next_state.env_rotation, u);
        }

        public virtual bool TryGetTimestampIndex(float t, out int index) {
            // Default: set index = 0
            index = 0;
            
            // Get all state data, terminate early if we don't have any state dta
            var state_data = session.state_data;
            if (state_data == null || state_data.Count == 0) {
                Debug.LogError("State data is null");
                return false;
            }

            // Handle before first sample
            if (t <= state_data[0].timestamp) {
                Debug.LogError("Prior to the first timestamp");
                return false;
            }
            // Handle after last sample
            if (t >= state_data[^1].timestamp) {
                Debug.LogError("After the last timestamp");
                index = state_data.Count - 1;
                return false;   
            }

            // We're now somewhere between this trajectory's first and last timestamp
            // So now we must interpolate and find the index where the timestamp fits between
            for (int i = 0; i < state_data.Count - 1; i++) {
                // Grab the current and the next trajectory point
                var a = state_data[i];
                var b = state_data[i + 1];
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

    }
}
