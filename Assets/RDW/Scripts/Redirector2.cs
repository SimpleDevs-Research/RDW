using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    public class Redirector2 : MonoBehaviour
    {
        [Header("=== Head and Environment ===")]
        [Tooltip("We need references to two core game objects:\n- head camera ('CenterEyeAnchor' in Meta SDK, 'Main Camera' in OpenXR), and\n- Environment root (contains everything in the virtual environment)")]
        public Transform head_ref;
        [Tooltip("We need references to two core game objects:\n- head camera ('CenterEyeAnchor' in Meta SDK, 'Main Camera' in OpenXR), and\n- Environment root (contains everything in the virtual environment)")]
        public Transform environment_ref;
        [Tooltip("The `head_ref` is actually offset by a little bit. We use a 2nd transform to track the position of the true head")]
        public Transform head_pos_ref;

        [Header("=== Hand Refs ===")]
        public Transform left_hand_ref, right_hand_ref;

        [Header("=== Gain Components ===")]
        public bool at_boundary = false;
        private bool prev_at_boundary = false;
        public UnityAction on_boundary_enabled;  // called when redirection is enabled
        public UnityAction on_boundary_disabled; // called when redirection is disabled
        public List<GainComponent2> default_gain_components = new List<GainComponent2>();
        public List<GainComponent2> boundary_gain_components = new List<GainComponent2>();

        [Header("=== READ-ONLY ===")]    
        public Vector3 true_head_displacement;
        public Vector3 pivot;

        private void OnDrawGizmos() {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(head_ref.TransformPoint(true_head_displacement), 0.1f);
        }

        public void Start() {
            pivot = head_ref.position;
            prev_at_boundary = at_boundary;
            foreach(GainComponent2 gc in default_gain_components) gc.Initialize(this);
            foreach(GainComponent2 gc in boundary_gain_components) gc.Initialize(this);
        }

        public void EstimateTrueHeadDisp() {
            // Assume that the user's hands 
            if (left_hand_ref == null || right_hand_ref == null) {
                Debug.Log("Cannot estimate true head displacement because of missing hand refs");
                return;
            }

            // Get the local positions of both hands
            Vector3 left_localPos = head_ref.InverseTransformPoint(left_hand_ref.position);
            Vector3 right_localPos = head_ref.InverseTransformPoint(right_hand_ref.position);
            // Calculate the Z position of both left and right (via averaging)
            float avg_z = (left_localPos.z + right_localPos.z)/2f;
            true_head_displacement = new Vector3(0, 0, avg_z);
            head_pos_ref.localPosition = true_head_displacement;
        }

        public void Update() {
            // Initialize yaw delta
            float yaw_delta = 0f;
            
            // Depending on if we're at the boundary or not... contribute to yaw_delta
            if (at_boundary) {
                foreach(GainComponent2 gc in boundary_gain_components) yaw_delta += gc.CalculateGain();
                // Boundary components do not change the pivot.
            }
            else {
                foreach(GainComponent2 gc in default_gain_components) yaw_delta += gc.CalculateGain();
                pivot = head_ref.TransformPoint(true_head_displacement); // Defaults have a changing pivot
            }

            // After calculatin the entire yaw delta, rotate the environment around the pivot point.
            environment_ref.RotateAround(pivot, Vector3.up, yaw_delta);
        }

        public void ToggleAtBoundary() { 
            at_boundary = !at_boundary;
            CheckAtBoundaryChange();
        }

        // Called when a value in the inspector is changed
        public void OnValidate() {
            if (!Application.isPlaying) return;
            CheckAtBoundaryChange();
        }

        private void CheckAtBoundaryChange() {
            if (prev_at_boundary != at_boundary) {
                // Change detected.
                if (at_boundary) {
                    on_boundary_enabled?.Invoke();
                    pivot = head_pos_ref.position;
                }
                else on_boundary_disabled?.Invoke();
            }
            prev_at_boundary = at_boundary;
        }
    }
}