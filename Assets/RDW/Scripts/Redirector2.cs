using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    public class Redirector2 : MonoBehaviour
    {
        public enum Direction { Left=-1, Right=1 }
        public enum PivotOrigin { Head, BoundaryBuffer }

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
        public float min_speed_threshold = 0.5f;
        public float max_speed_threshold = 1.5f;
        public PivotOrigin pivotOrigin = PivotOrigin.Head;
        public float boundary_buffer = 0.5f;
        public bool dynamic_goal_direction = true;
        public List<GainComponent2> gain_components = new List<GainComponent2>();
        public Direction goal_direction = Direction.Left;

        [Header("=== Cached - READ-ONLY ===")]
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
        [Tooltip("The signed angle representing the horizontal rotation of the head in world space.")]
        public float current_head_rotation;
        [Tooltip("The redirection... direction factor. Some gain components may use this.")]
        public float direction_factor = 1f;
        [Tooltip("The speed factor to control redirection while standing still vs. moving. Some gain components may use this.")]
        public float speed_factor = 0f;
        [Tooltip("The pivot position where the environment is rotating around")]
        public Vector3 pivot = Vector3.zero;

        public void Start() {
            prev_position = head_ref.position.Flatten();
            CacheCurrent(float.MaxValue);
            CachePrev();
            foreach(GainComponent2 gc in gain_components) gc.Initialize(this);
        }

        public void Update() {
            // Get the current delta time
            float deltaTime = Time.deltaTime;

            // Measure the current frame
            CacheCurrent(deltaTime);

            // Initialize yaw delta, and use each gain component to contribute to it.
            float yaw_delta = 0f;
            foreach(GainComponent2 gc in gain_components) yaw_delta += gc.CalculateGain(deltaTime);
            // After calculating the entire yaw delta, rotate the environment around the pivot point.
            environment_ref.RotateAround(pivot, Vector3.up, yaw_delta);

            // Cache the current into the previous for the next frame
            CachePrev();
        }

        private void CacheCurrent(float deltaTime) {
            current_position = head_ref.position.Flatten();
            current_displacement = current_position - prev_position;
            current_move_direction = current_displacement.normalized;
            current_orientation = Vector3.Normalize(head_ref.forward.Flatten());
            current_head_rotation = Vector3.SignedAngle(prev_orientation, current_orientation, Vector3.up);
            if (dynamic_goal_direction) {
                float dir_dot = Vector3.Dot(
                    SpatialManager.Instance.worldCenter-head_ref.position.Flatten(), 
                    head_ref.right.Flatten()
                );
                goal_direction = (dir_dot < 0f) ? Direction.Left : Direction.Right;
            }
            direction_factor = (float)((int)goal_direction);
            speed_factor = Mathf.Clamp(((current_displacement.magnitude/deltaTime)-min_speed_threshold)/(max_speed_threshold-min_speed_threshold), 0f, 1f);
            
            if (pivotOrigin == PivotOrigin.BoundaryBuffer) {
                // Note that displacement might be 0. We add the denominator by a small number to avoid 0 denominator
                float radius = current_displacement.magnitude / (Mathf.Abs(current_head_rotation)+0.0001f);
                Vector3 pivotDir = direction_factor * head_ref.right.Flatten();
                pivot = head_ref.position.Flatten() + pivotDir * radius;
            } else {
                pivot = head_ref.position.Flatten();
            }
        }
        private void CachePrev() {
            prev_position = current_position;
            prev_displacement = current_displacement;
            prev_move_direction = current_move_direction;
            prev_orientation = current_orientation;
        }

        public void ToggleGainComponents() {
            foreach(GainComponent2 gc in gain_components) gc.Toggle();
        }

        public void TogglePivotOrigin() {
            pivotOrigin = (pivotOrigin == PivotOrigin.Head) ? PivotOrigin.BoundaryBuffer : PivotOrigin.Head;
        }
        
    }
}