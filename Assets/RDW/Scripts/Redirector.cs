using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    public class Redirector : MonoBehaviour
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
        [Tooltip("It's expected that the eye tracking (insofar with Meta SDK) is captured as a Transform." +
                    "If using a Quest Pro, make sure to have the proper eye tracking setup done and" + 
                    "assign the Transform for your chosen eye here.")]
        public Transform eye_ref;

        [Header("=== Gain Components ===")]
        public float min_speed_threshold = 0.5f;
        public float max_speed_threshold = 1.5f;
        public PivotOrigin pivotOrigin = PivotOrigin.Head;
        public float boundary_buffer = 0.5f;
        public bool dynamic_goal_direction = true;
        public List<GainComponent> gain_components = new List<GainComponent>();
        public Direction goal_direction = Direction.Left;

        [Header("=== Cached - READ-ONLY ===")]
        // ===
        private Vector3 prev_position, prev_head_orientation, prev_eye_orientation;
        private float prev_yaw_delta = 0f;
        [Tooltip("The position of the user in world space.")] 
        public Vector3 current_position;
        [Tooltip("The translation direction of the user in world space.")]
        public Vector3 current_displacement;
        [Tooltip("Same as `current_displacement`, except as a normalized vector")]
        public Vector3 current_move_direction;
        [Tooltip("The forward direction of the user's head in world space.")]
        public Vector3 current_head_orientation;
        [Tooltip("The forward direction of the user's eye in head local space")]
        public Vector3 current_eye_orientation;
        [Tooltip("The signed angle representing the horizontal rotation of the head in world space.")]
        public float current_head_rotation;
        [Tooltip("The absolute angle delta of the eye tracker, if present")]
        public float current_eye_rotation;
        [Tooltip("The yaw delta induced by RDW")]
        public float current_yaw_delta = 0f;
        [Tooltip("The redirection... direction factor. Some gain components may use this.")]
        public float direction_factor = 1f;
        [Tooltip("The speed factor to control redirection while standing still vs. moving. Some gain components may use this.")]
        public float speed_factor = 0f;
        [Tooltip("The pivot position where the environment is rotating around")]
        public Vector3 pivot = Vector3.zero;
        

        public void Start() {
            prev_position = head_ref.position.Flatten();
            prev_yaw_delta = 0f;
            prev_eye_orientation = (eye_ref != null) ? head_ref.InverseTransformDirection(eye_ref.forward) : Vector3.zero;
            CacheCurrent(float.MaxValue);
            CachePrev();
            foreach(GainComponent gc in gain_components) gc.Initialize(this);
        }

        public void Update() {
            // Get the current delta time
            float deltaTime = Time.deltaTime;

            // Measure the current frame
            CacheCurrent(deltaTime);

            // Initialize yaw delta, and use each gain component to contribute to it.
            // current_yaw_delta = 0f;  // Note that we set this to 0 in `CacheCurrent()` anyways.
            foreach(GainComponent gc in gain_components) current_yaw_delta += gc.CalculateGain(deltaTime);
            // After calculating the entire yaw delta, rotate the environment around the pivot point.
            environment_ref.RotateAround(pivot, Vector3.up, current_yaw_delta);

            // Cache the current data into the previous for the next frame
            CachePrev();
        }

        private void CacheCurrent(float deltaTime) {
            // Position is a constant in world space
            current_position = head_ref.position.Flatten();
            // Displacement is the vector representing how much the player has moved since the last frame
            current_displacement = current_position - prev_position;
            // The (normalized) direction of the displacement.
            current_move_direction = current_displacement.normalized;
            // NOT A ROTATION. The vector representing the head's current forward direction in world space
            current_head_orientation = Vector3.Normalize(head_ref.forward.Flatten());
            // Head rotation is how much the user's head has rotated since the last frame.
            //  Note that we subtract the amount of yaw rotation induced by RDW from the previous frame.
            current_head_rotation = Vector3.SignedAngle(prev_head_orientation, current_head_orientation, Vector3.up) - prev_yaw_delta;
            // Eye rotation is how much the user's eye has rotated since the last frame
            //  Note that we try to do things locally to the head, which should already account for RDW rotation by proxy
            current_eye_orientation = (eye_ref != null) ? head_ref.InverseTransformDirection(eye_ref.forward) : Vector3.zero;
            current_eye_rotation = Vector3.Angle(prev_eye_orientation, current_eye_orientation);
            // Set the current yaw delta of this frame to 0
            current_yaw_delta = 0f;

            // We want to track the direction we want the RDW to head to. We calculate that here.
            //  - If using dynamic goal direction (the default), then the intended redirection will
            //      always point to the center defined by `SpatialManager`.
            if (dynamic_goal_direction) {
                float dir_dot = Vector3.Dot(
                    SpatialManager.Instance.worldCenter-head_ref.position.Flatten(), 
                    head_ref.right.Flatten()
                );
                goal_direction = (dir_dot < 0f) ? Direction.Left : Direction.Right;
            }
            // Given the goal direction, calculate the direction factor
            direction_factor = (float)((int)goal_direction);
            // Speed factor controls how much the RDW affects the player depending on their movement speed.
            //      This allows you to control if RDW affects the user while they're standing still, for example.
            speed_factor = Mathf.Clamp(((current_displacement.magnitude/deltaTime)-min_speed_threshold)/(max_speed_threshold-min_speed_threshold), 0f, 1f);

            // The pivot is where the user's pivot point is for RDW.
            //      By default, the pivot is just the user's head position in world space.
            //      However, some gain components expect the pivot to be dependent on the user's own movement.
            //      This dynamic pivot moves the pivot left or right based on RDW direction and how much they displace/rotate their body. 
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
            prev_head_orientation = current_head_orientation;
            prev_eye_orientation = current_eye_orientation;
            prev_yaw_delta = current_yaw_delta;
        }

        public void ToggleGainComponents() {
            foreach(GainComponent gc in gain_components) gc.Toggle();
        }

        public void TogglePivotOrigin() {
            pivotOrigin = (pivotOrigin == PivotOrigin.Head) ? PivotOrigin.BoundaryBuffer : PivotOrigin.Head;
        }
        
    }
}