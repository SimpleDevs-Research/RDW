using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Helpers;

namespace RDW {
    public class Redirector2 : MonoBehaviour
    {
        public static Redirector2 Instance;

        public enum Direction { Left=-1, Right=1 }
        public enum PivotOrigin { Head, BoundaryBuffer }

        [System.Flags]
        public enum PlayerState : int { 
            Off         = 0x00,
            Standing    = 0x01,
            Walking     = 0x02, 
            AtBoundary  = 0x04,
        }

        [Header("=== Referenecs ===")]
        [Tooltip("The environment parent is a game object parent that contains all the objects in the virtual environment.")]
        public Transform environmentParent;
        [Tooltip("This is the gain settings associated with this. The RDW gain setting is priority, so this is just a reference. It WILL be overwritten, so don't bother setting it in the inspector.")]
        [SerializeField] private GainSettings gainSettings;

        [Header("=== Gain Components ===")]
        [Tooltip("The minimum speed expected for a player in motion. If the user's speed is smaller than this, then they're classified as `Standing`.")]
        public float min_speed_threshold = 0.5f;
        [Tooltip("The maximum speed expected for a player in motion. This provides a cap for any speed-depending gain modules.")]
        public float max_speed_threshold = 1.5f;
        [Tooltip("What should be considered the pivot point of the player during gain calculation?")]
        public PivotOrigin pivotOrigin = PivotOrigin.Head;
        [Tooltip("Do we want the redirection direction to change dynamically? Or keep it static?")]
        public bool dynamic_goal_direction = true;
        [Tooltip("Allows you to define a goal direction of choice, if NOT using dynamic goal direction. If you are using dynamic direction, then this toggle shouldn't change.")]
        public Direction goal_direction = Direction.Left;

        [Header("=== Caching and Saving Records ===")]
        public JSONWriter json_writer;
        public bool write_log = true;
        private Session log_session;

        [Space]
        [Header("=== Cached - READ-ONLY ===")]
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
        [Tooltip("The translational delta induced by RDW")]
        public Vector3 current_translation_delta = Vector3.zero;
        [Tooltip("The redirection... direction factor. Some gain components may use this.")]
        public float direction_factor = 1f;
        [Tooltip("The speed factor to control redirection while standing still vs. moving. Some gain components may use this.")]
        public float speed_factor = 0f;
        [Tooltip("The pivot position where the environment is rotating around")]
        public Vector3 pivot = Vector3.zero;
        [Tooltip("What's the current state of the player?")]
        public PlayerState playerState = PlayerState.Standing;
        
        private void Awake() {
            Instance = this;
        }

        public void Activate() {
            // Grab gain settings ref from RDW
            gainSettings = RDW.Instance.settings;

            // Cache the starting data
            prev_position = RDW.Instance.headPoseAnchor.position.Flatten();
            prev_yaw_delta = 0f;
            prev_eye_orientation = (RDW.Instance.eyeGaze != null) ? RDW.Instance.headPoseAnchor.InverseTransformDirection(RDW.Instance.eyeGaze.forward) : Vector3.zero;
            CacheCurrent(float.MaxValue);
            CachePrev();
            Debug.Log("Initial Caching Finished!");

            // Prepare all gain modules from gainSettings. We need to iterate through all of them.
            List<string> gain_modules = new List<string>();
            if (gainSettings.curvatureGain.enabled) {
                gainSettings.curvatureGain.Enable();
                gain_modules.Add("curvature");
            }
            if (gainSettings.rotationGain.enabled) {
                gainSettings.rotationGain.Enable();
                gain_modules.Add("rotation");
            }
            if (gainSettings.saccadeGain.enabled) {
                gainSettings.saccadeGain.Enable();
                gain_modules.Add("saccade");
            }
            if (gainSettings.manualGain.enabled) {
                gainSettings.manualGain.Enable();
                gain_modules.Add("manual");
            }
            Debug.Log("Gain Module Initialization Finished!");

            // Should we toggle passthrough?
            RDW.Instance.TogglePassthrough(gainSettings.usePassthrough);

            // Prep log if needed
            if (write_log) {
                // We need to update `json_writer` by setting the file and dir name
                string dt = Helpers.SaveSystemMethods.GetCurrentDateTime();
                json_writer.fileName = gainSettings.sceneName + "_" + dt;
                json_writer.dirName = RDW.Instance.id;
                // We initialize the writer to prep it
                json_writer.Initialize();
                // `log_session` holds our session data
                log_session = new Session {
                    participantID = RDW.Instance.id,    // Participant's unique ID
                    sessionTimestamp = dt,              // Datetime when the session was conducted
                    sessionStart = Time.time,           // The time since the beginning of the game when the session starts
                    sessionEnd = Time.time,             // The time since the beginning of the game when the session ends
                    duration = 0f,                      // How long the session took to complete - WE'LL FILL THIS LATER
                    sceneName = gainSettings.sceneName, // The name of the scene that was loaded
                    worldCenter = RDW.Instance.worldCenter, // The world location of the play space, derived from Boundary
                    playSpaceSize = Boundary.Instance.size, // The XZ plane scale of the play space, derived from Boundary
                    boundaryApproachDist = Boundary.Instance.approachingDistance, // The distance the user was comfortable approaching the Boundary edge
                    headPoseOffset = Player.Instance.headPoseOffset,    // The local space offset of the detected head pose
                };
            }
            Debug.Log("JSON Log initialization Finished!");

            // Set this to be enabled
            this.enabled = true;
        }

        public void Deactivate() {
            this.enabled = false;
            if (gainSettings.curvatureGain.enabled) gainSettings.curvatureGain.Disable();
            if (gainSettings.rotationGain.enabled)  gainSettings.rotationGain.Disable();
            if (gainSettings.saccadeGain.enabled)   gainSettings.saccadeGain.Disable();
            if (gainSettings.manualGain.enabled)    gainSettings.manualGain.Disable();
            SaveData();
        }

        public void Update() {
            // Get the current delta time
            float deltaTime = Time.deltaTime;

            // Measure the current frame
            CacheCurrent(deltaTime);
            Vector3 activePivot = pivot;
            CalculatePlayerState(deltaTime);
            Debug.Log("Update: Current cached");

            // We ONLY update our gain if `environmentParent` is not null
            if (environmentParent != null) {
                // Initialize yaw delta, and use each gain component to contribute to it.
                // current_yaw_delta = 0f;  // Note that we set this to 0 in `CacheCurrent()` anyways.
                if (gainSettings.curvatureGain.enabled) {
                    current_yaw_delta += gainSettings.curvatureGain.CalculateGain(this, deltaTime);
                }
                if (gainSettings.rotationGain.enabled) {
                    current_yaw_delta += gainSettings.rotationGain.CalculateGain(this, deltaTime);
                }
                if (gainSettings.saccadeGain.enabled) {
                    current_yaw_delta += gainSettings.saccadeGain.CalculateGain(this, deltaTime);
                }
                if (gainSettings.manualGain.enabled) {
                    current_yaw_delta += gainSettings.manualGain.CalculateGain(this, deltaTime);
                    if (gainSettings.manualGain.active) {
                        //activePivot = gainSettings.manualGain.GetLockedPivot();
                        activePivot = Player.Instance.CurrentState.Pivot;
                    }
                }
                Debug.Log("Update: All gain components contributed to gain");

                // After calculating the entire yaw delta, rotate the environment around the pivot point.
                environmentParent.RotateAround(activePivot, Vector3.up, current_yaw_delta);
                // After rotation, we also apply translational gain
                environmentParent.position += current_translation_delta;
                Debug.Log("Environment Adjusted");
            }

            // Cache the current data into the previous for the next frame
            CachePrev();
            Debug.Log("Update: Previous cached");

            // If logging, save
            if (write_log && json_writer.is_active) AddLogState(deltaTime, activePivot);
        }

        private void CacheCurrent(float deltaTime) {
            // Position is a constant in world space
            current_position = RDW.Instance.headPoseAnchor.position.Flatten();

            // Displacement is the vector representing how much the player has moved since the last frame
            current_displacement = current_position - prev_position;
            
            // The (normalized) direction of the displacement.
            current_move_direction = current_displacement.normalized;
            
            // NOT A ROTATION. The vector representing the head's current forward direction in world space
            current_head_orientation = Vector3.Normalize(RDW.Instance.headPoseAnchor.forward.Flatten());
           
            // Head rotation is how much the user's head has rotated since the last frame.
            //  Note that we subtract the amount of yaw rotation induced by RDW from the previous frame.
            current_head_rotation = Vector3.SignedAngle(prev_head_orientation, current_head_orientation, Vector3.up) - prev_yaw_delta;
            
            // Eye rotation is how much the user's eye has rotated since the last frame
            //  Note that we try to do things locally to the head, which should already account for RDW rotation by proxy
            current_eye_orientation = (RDW.Instance.eyeGaze != null) ? RDW.Instance.headPoseAnchor.InverseTransformDirection(RDW.Instance.eyeGaze.forward) : Vector3.zero;
            current_eye_rotation = Vector3.Angle(prev_eye_orientation, current_eye_orientation);
            
            // Set the current yaw delta of this frame to 0
            current_yaw_delta = 0f;

            // We want to track the direction we want the RDW to head to. We calculate that here.
            //  - If using dynamic goal direction (the default), then the intended redirection will
            //      always point to the center defined by `SpatialManager`.
            if (dynamic_goal_direction) {
                float dir_dot = Vector3.Dot(
                    RDW.Instance.worldCenter-RDW.Instance.headPoseAnchor.position.Flatten(), 
                    RDW.Instance.headPoseAnchor.right.Flatten()
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
                Vector3 pivotDir = direction_factor * RDW.Instance.headPoseAnchor.right.Flatten();
                pivot = RDW.Instance.headPoseAnchor.position.Flatten() + pivotDir * radius;
            } else {
                pivot = RDW.Instance.headPoseAnchor.position.Flatten();
            }
        }
        private void CachePrev() {
            prev_position = current_position;
            prev_head_orientation = current_head_orientation;
            prev_eye_orientation = current_eye_orientation;
            prev_yaw_delta = current_yaw_delta;
        }

        // Make sure to call this after calling `CacheCurrent()`
        private void CalculatePlayerState(float deltaTime) {
            // Calculate speed at this frame. Depending on the speed, set the walking vs standing status
            if (Player.Instance.CurrentState.Translating == Player.TranslationStatus.Stationary) {
                playerState |= PlayerState.Standing;
                playerState &= ~PlayerState.Walking;
            } else {
                playerState |= PlayerState.Walking;
                playerState &= ~PlayerState.Standing;
            }

            // Depending on if we're at the boundary (specifically at warning), we toggle this flag on or off
            if (Boundary.Instance.playerInfo.Status != Boundary.BoundaryStatus.Within) {
                playerState |= PlayerState.AtBoundary;
            } else {
                playerState &= ~PlayerState.AtBoundary;
            }
        }

        private void AddLogState(float dt, Vector3 activePivot) {
            // Pre-cache data
            Vector3 pos = RDW.Instance.headPoseAnchor.position;
            Vector3 forward = RDW.Instance.headPoseAnchor.forward;
            Quaternion rot = RDW.Instance.headPoseAnchor.rotation;
            // New struct
            State s = new State {
                frame = Time.frameCount,
                timestamp = Time.time - log_session.sessionStart,
                deltaTime = dt,

                playerWorldPosition = pos,
                playerWorldForward = forward,
                playerWorldRotation = rot,
            
                playerPlaySpacePosition = Boundary.Instance.GetLocalPosition(pos),
                playerPlaySpaceForward = Boundary.Instance.GetLocalDirection(forward),
                playerPlaySpaceRotation = Boundary.Instance.GetLocalRotation(rot),

                playerEnvPosition = Environment.Current.GetLocalPositionInEnv(pos),
                playerEnvForward = Environment.Current.GetLocalDirectionInEnv(forward),
                playerEnvRotation = Environment.Current.GetLocalRotationInEnv(rot),

                envPosition = Environment.Current.envPosition,
                envRotation = Environment.Current.envRotation,

                curvatureActive = gainSettings.curvatureGain.active,
                curvatureContribution = gainSettings.curvatureGain.contribution,
                rotationActive = gainSettings.rotationGain.active,
                rotationContribution = gainSettings.rotationGain.contribution,
                saccadeActive = gainSettings.saccadeGain.active,
                saccadeContribution = gainSettings.saccadeGain.contribution,
                manualActive = gainSettings.manualGain.active,
                manualContribution = gainSettings.manualGain.contribution,
                
                finalContribution = current_yaw_delta,
                pivot = activePivot,
                playerBoundaryState = Boundary.Instance.boundaryStatusStr,
                playerTranslating = Player.Instance.CurrentState.Translating.ToString(),
                playerTurning = Player.Instance.CurrentState.Turning.ToString()
            };
            // Adding to log session
            log_session.sessionData.Add(s);
        }

        private void SaveData() {
            if (write_log && json_writer.is_active) {
                log_session.sessionEnd = Time.time;
                log_session.duration = log_session.sessionEnd - log_session.sessionStart;
                if (json_writer.SaveJSON(JSONWriter.ConvertToJSON<Session>(log_session))) {
                    json_writer.Disable();
                }
            }
        }

        public void TogglePivotOrigin() {
            pivotOrigin = (pivotOrigin == PivotOrigin.Head) ? PivotOrigin.BoundaryBuffer : PivotOrigin.Head;
        }

        private void OnApplicationPause(bool pauseStatus) {
            SaveData();
        }

        private void OnApplicationQuit() {
            SaveData();
        }

        private void OnDisable() {
            SaveData();
        }

        private void OnDestroy() {
            SaveData();
        }
        
    }
}