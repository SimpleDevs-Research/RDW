using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class Playback : MonoBehaviour
    {

        [Header("=== References ===")]
        public Transform head_ref;
        public Transform environment_ref;

        [Header("=== Data Loaded ===")]
        public TextAsset json = null;
        public Session session = null;

        [Header("=== Controls ===")]
        public bool playing = false;
        public float time = 0f;
        public float speed = 1f;
        public float maxTime;

        private void Start() {
            // Attempt to read the JSON file
            session = JsonUtility.FromJson<Session>(json.text);
            maxTime = GetMaxTime(session);
        }

        public float GetMaxTime(Session s) {
            float max_timestamp = 0f;
            foreach(State st in s.state_data) {
                float timestamp = st.timestamp;
                max_timestamp = Mathf.Max(max_timestamp, timestamp);
            }
            return max_timestamp;
        }

        private void Update() {
            // If we're not playing or if our instance is not set, then end early
            if (!playing || session == null) return;
            // Adjust the current timestamp based on delta time
            time += Time.deltaTime * speed;
            // Loop back to 0 if we reach the end
            if (time > maxTime) time = 0f;
        }
    }
}
