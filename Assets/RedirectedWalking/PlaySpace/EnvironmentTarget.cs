using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    [RequireComponent(typeof(Rigidbody))]
    public class EnvironmentTarget : MonoBehaviour
    {
        [Header("=== Position Handling ===")]
        public UnityEvent onTargetReached;

        [Header("=== Orientation Handling ===")]
        public bool checkOrientation = false;
        public float orientationThreshold = 30f;

        private bool reached = false;
        private bool oriented = false;
        private bool completed = false;

        private void OnEnable() {
            completed = false;
            oriented = false;
            reached = false;
            if (!checkOrientation) oriented = true;
        }

        private void OnTriggerEnter(Collider other) {
            reached = true;
            if (oriented && !completed) {
                completed = true;
                onTargetReached?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other) {
            reached = false;
        }

        private void Update() {
            if (completed) return;
            
            // Check orientation
            if (checkOrientation && Player.Instance != null) {
                Vector2 flattenedForward = new Vector2(
                    transform.forward.x, 
                    transform.forward.z
                ).normalized;
                Vector2 playerForward = new Vector2(
                    Player.Instance.headPoseAnchor.forward.x, 
                    Player.Instance.headPoseAnchor.forward.z
                ).normalized;
                
                float flatAngle = Vector2.Angle(flattenedForward, playerForward);
                oriented = flatAngle <= orientationThreshold;
                if (oriented && reached && !completed) {
                    completed = true;
                    onTargetReached?.Invoke();
                }
            }
        }
    }

}