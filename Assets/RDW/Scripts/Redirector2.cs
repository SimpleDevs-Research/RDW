using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace RDW {
    public class Redirector2 : MonoBehaviour
    {
        public enum PivotRef { Head=0, Left_Hand=1, Right_Hand=2 }

        [Header("=== Head, Hands, Environment ===")]
        [Tooltip("We need references to two core things: 1) the user's head, and the environment root.\n"
                    + "The user's head can be either the head camera ('CenterEyeAnchor' in Meta SDK, 'Main Camera' in OpenXR), or the `head_pos_ref` of a `Calibration` component in the scene.")]
        public Transform head_ref;
        [Tooltip("We need references to two core things: 1) the user's head, and the environment root.\n"
                    + "The environment root is a game object parent that contains all the objects in the virtual environment.")]
        public Transform environment_ref;
        [Tooltip("We need references to the left and right hand of the user.")]
        public Transform left_hand_ref, right_hand_ref;

        [Header("=== Gain Components ===")]
        public float boundary_buffer = 0.5f;
        public bool at_boundary = false;
        private bool prev_at_boundary = false;
        public UnityAction on_boundary_enabled;  // called when redirection is enabled
        public UnityAction on_boundary_disabled; // called when redirection is disabled
        public List<GainComponent2> default_gain_components = new List<GainComponent2>();
        public List<GainComponent2> boundary_gain_components = new List<GainComponent2>();

        [Header("=== Debug ===")]
        public TextMeshPro debugTextbox;

        [Header("=== Cached - READ-ONLY ===")]
        public int pivot_ref_id = 0;
        public Vector3 pivot;
        // ===
        private Vector3 prev_position, prev_displacement, prev_move_direction, prev_orientation;
        [Tooltip("The position of the user in world space.")] 
        public Vector3 current_position;
        [Tooltip("The translation direction of the user in world space.")]
        public Vector3 current_displacement;
        [Tooltip("Same as `current_displacement`, except as a normalized vector")]
        public Vector3 current_move_direction;
        [Tooltip("The forward direction of the user's head in world space.")]
        public Vector3 current_orientation;

        public void Start() {
            pivot = head_ref.position;
            prev_at_boundary = at_boundary;
            prev_position = head_ref.position.Flatten();
            CacheCurrent();
            CachePrev();
            foreach(GainComponent2 gc in default_gain_components) gc.Initialize(this);
            foreach(GainComponent2 gc in boundary_gain_components) gc.Initialize(this);
        }

        public void Update() {
            // Measure the current frame
            CacheCurrent();

            // Initialize yaw delta
            float yaw_delta = 0f;
            
            // Depending on if we're at the boundary or not... contribute to yaw_delta
            if (at_boundary) {
                foreach(GainComponent2 gc in boundary_gain_components) yaw_delta += gc.CalculateGain();
                switch(pivot_ref_id) {
                    case 1:
                        pivot = left_hand_ref.position;
                        break;
                    case 2:
                        pivot = right_hand_ref.position;
                        break;
                    default:
                        pivot = head_ref.position;
                        break;
                }
            }
            else {
                foreach(GainComponent2 gc in default_gain_components) yaw_delta += gc.CalculateGain();
                pivot = head_ref.position; // Defaults have a changing pivot
            }

            // After calculatin the entire yaw delta, rotate the environment around the pivot point.
            environment_ref.RotateAround(pivot, Vector3.up, yaw_delta);

            // Cache the current into the previous for the next frame
            CachePrev();

            if (debugTextbox != null) {
                debugTextbox.text = $"{at_boundary}";
            }
        }

        public void ToggleAtBoundary() { 
            at_boundary = !at_boundary;
            pivot_ref_id = 0;
            CheckAtBoundaryChange();
        }
        public void ToggleAtBoundary(PivotRef pivot_ref) {
            at_boundary = !at_boundary;
            pivot_ref_id = (int)pivot_ref;
            CheckAtBoundaryChange();
        }
        public void ToggleAtBoundary(int pivot_ref) {
            at_boundary = !at_boundary;
            pivot_ref_id = pivot_ref;
            CheckAtBoundaryChange();
        }

        // Called when a value in the inspector is changed
        public void OnValidate() {
            if (!Application.isPlaying) return;
            // By default, pivot around the head
            pivot_ref_id = (int)PivotRef.Head;
            CheckAtBoundaryChange();
        }

        private void CheckAtBoundaryChange() {
            if (prev_at_boundary != at_boundary) {
                // Change detected.
                if (at_boundary) on_boundary_enabled?.Invoke();
                else on_boundary_disabled?.Invoke();
            }
            prev_at_boundary = at_boundary;
        }

        private void CacheCurrent() {
            current_position = head_ref.position.Flatten();
            current_displacement = current_position - prev_position;
            current_move_direction = current_displacement.normalized;
            current_orientation = Vector3.Normalize(head_ref.forward.Flatten());
        }
        private void CachePrev() {
            prev_position = current_position;
            prev_displacement = current_displacement;
            prev_move_direction = current_move_direction;
            prev_orientation = current_orientation;
        }
        
    }
}