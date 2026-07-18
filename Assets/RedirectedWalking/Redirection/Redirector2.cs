using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Helpers;
using RVO;

namespace RDW {
    public class Redirector2 : MonoBehaviour
    {
        public static Redirector2 Instance;

        [System.Flags]
        public enum PlayerState : int { 
            Off         = 0x00,
            Standing    = 0x01,
            Walking     = 0x02, 
            AtBoundary  = 0x04,
        }

        [Header("=== References ===")]
        [Tooltip("The environment parent is a game object parent that contains all the objects in the virtual environment.")]
        public Transform environmentParent;
        [Tooltip("This is the gain settings associated with this. The RDW gain setting is priority, so this is just a reference. It WILL be overwritten, so don't bother setting it in the inspector.")]
        [SerializeField] private GainSettings gainSettings;
        [SerializeField] private List<RVO.NonAgent> rvo_non_agents = new();

        [Header("=== Gain Components ===")]
        [Tooltip("The minimum speed expected for a player in motion. If the user's speed is smaller than this, then they're classified as `Standing`.")]
        public float min_speed_threshold = 0.5f;
        [Tooltip("The maximum speed expected for a player in motion. This provides a cap for any speed-depending gain modules.")]
        public float max_speed_threshold = 1.5f;
        [Tooltip("Do we want the redirection direction to change dynamically? Or keep it static?")]
        public Steering steering;
        [Tooltip("Allows you to define a goal direction of choice, if NOT using dynamic goal direction. If you are using dynamic direction, then this toggle shouldn't change.")]
        public Steering.Direction goal_direction = Steering.Direction.Left;

        [Header("=== Caching and Saving Records ===")]
        public JSONWriter json_writer;
        public bool write_log = true;
        private Session log_session;

        [Space]
        [Header("=== Cached - READ-ONLY ===")]
        private Vector3 prev_head_orientation, prev_eye_orientation;
        private float prev_yaw_delta = 0f;
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
        [Tooltip("The \"CURRENT\" pivot. Could be `pivot`, but could also be something else.")]
        public Vector3 current_pivot = Vector3.zero;
        [Tooltip("What's the current state of the player?")]
        public PlayerState playerState = PlayerState.Standing;
        
        private void Awake() {
            Instance = this;
        }

        // =========================================
        // === Redirection Activation ===
        // When we want to start redirection, we call this.
        // The activation function does several things:
        //  1. Sets the gain settings for the current environment
        //  2. Caches both the current state and the previous state. This cached data is used to calculate 
        //      changes between frames, such as displacement
        //  3. Enable all gain functions to be used in the current environment
        //  4. Toggle passthrough if needed
        //  5. Initlize the logger, if prompted.
        // =========================================
        public void Activate() {
            // Grab gain settings ref from RDW
            gainSettings = RDW.Instance.settings;

            // Cache the starting data
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

            // How do we initialize steering?
            switch(gainSettings.steeringType) {
                case Steering.SteeringType.Manual:
                    steering = new ManualSteering();
                    break;
                case Steering.SteeringType.S2C:
                    steering = new S2C();
                    break;
                default:
                    steering = new Steering();
                    break;
            }

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
        
        // =========================================
        // === Redirection Deactivation ===
        // When we no longer need the redirection, then we call this.
        //  1. Disable this component
        //  2. Disable all active gains
        //  3. Save our logged data, if needed.
        // =========================================
        public void Deactivate() {
            this.enabled = false;
            if (gainSettings.curvatureGain.enabled) gainSettings.curvatureGain.Disable();
            if (gainSettings.rotationGain.enabled)  gainSettings.rotationGain.Disable();
            if (gainSettings.saccadeGain.enabled)   gainSettings.saccadeGain.Disable();
            if (gainSettings.manualGain.enabled)    gainSettings.manualGain.Disable();
            SaveData();
        }

        // =========================================
        // === Redirection Update Loop ===
        //  This is the core update loop that needs to be executed whenever redirection is needed.
        //  1. Update our current state cache
        //  2. If the environment is set, then we calcualte each enabled gain
        //  3. Cache the current state into the previous state
        //  4. Add data to logger, if we are logging.
        //  We don't actually modify the environment for redirection in `Update()`. We do that in `LateUpdate()`.
        //  This is to prevent jittering caused by the camera pose in VR moving in the same update cycle as the environment.
        // =========================================
        public void LateUpdate() {
            // Get the current delta time
            float deltaTime = Time.deltaTime;

            // Measure the current frame
            CacheCurrent(deltaTime);
            current_pivot = RDW.Instance.headPoseAnchor.position.Flatten();
            current_translation_delta = Vector3.zero;
            CalculatePlayerState(deltaTime);

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
                        //current_pivot = Player.Instance.CurrentState.Pivot;
                    }
                }

                // After calculating the entire yaw delta, rotate the environment around the pivot point.
                environmentParent.RotateAround(current_pivot, Vector3.up, current_yaw_delta);
                // After rotation, we also apply translational gain
                environmentParent.position += current_translation_delta;
                // We rotate the skybox to adhere to our environmentParent's rotation
                RenderSettings.skybox.SetFloat(
                    "_Rotation",
                    -environmentParent.eulerAngles.y
                );
            }

            // Cache the current data into the previous for the next frame
            CachePrev();

            // If logging, save
            if (write_log && json_writer.is_active) AddLogState(deltaTime, current_pivot);
        }

        // =========================================
        // === Redirection Late Update Loop ===
        //  After the `Update()` calculates the amount of gain needed, we rotate the environment round the pivot.
        //  We also update the skybox to match the rotation of the environment.
        // =========================================
        /*
        private void LateUpdate() {
            if (environmentParent != null) {
                // After calculating the entire yaw delta, rotate the environment around the pivot point.
                environmentParent.RotateAround(current_pivot, Vector3.up, current_yaw_delta);
                // After rotation, we also apply translational gain
                environmentParent.position += current_translation_delta;
                // We rotate the skybox to adhere to our environmentParent's rotation
                RenderSettings.skybox.SetFloat(
                    "_Rotation",
                    -environmentParent.eulerAngles.y
                );
            }
        }
        */


        // =========================================
        // === Redirection Current State Caching ===
        //  This is called in the `Update()` loop first thing. We cache the following:
        //  - [1] 2D head position in the current frame.
        //  - [2] Changes since the last frame: [2a] displacement, [2b] head rotation, [2c] eye rotation 
        // 2. Caches displacement features: 2D translation displacement from the last frame and the implied direction from that displacement.
        //  3. We also track the head's 2D orientation. This is a vector, not a quaternion.
        //  4. As well as 
        // =========================================
        private void CacheCurrent(float deltaTime) {
            // Head rotation is how much the user's head has rotated since the last frame.
            //  Note that we subtract the amount of yaw rotation induced by RDW from the previous frame.
            current_head_rotation = Vector3.SignedAngle(Player.Instance.PreviousState.Forward, Player.Instance.CurrentState.Forward, Vector3.up) - prev_yaw_delta;
            
            // Eye rotation is how much the user's eye has rotated since the last frame
            //  Note that we try to do things locally to the head, which should already account for RDW rotation by proxy
            current_eye_orientation = (RDW.Instance.eyeGaze != null) ? RDW.Instance.headPoseAnchor.InverseTransformDirection(RDW.Instance.eyeGaze.forward) : Vector3.zero;
            current_eye_rotation = Vector3.Angle(prev_eye_orientation, current_eye_orientation);
            
            // Set the current yaw delta of this frame to 0
            current_yaw_delta = 0f;

            // We want to track the direction we want the RDW to head to.
            goal_direction = steering.GetDirection();
            // Given the goal direction, calculate the direction factor
            direction_factor = (float)((int)goal_direction);
            // Speed factor controls how much the RDW affects the player depending on their movement speed.
            //      This allows you to control if RDW affects the user while they're standing still, for example.
            speed_factor = Mathf.Clamp(((Player.Instance.CurrentState.MoveDistance/deltaTime)-min_speed_threshold)/(max_speed_threshold-min_speed_threshold), 0f, 1f);
        }

        private void CachePrev() {
            //prev_head_orientation = current_head_orientation;
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
            
                playerPlaySpacePosition = Boundary.Instance.GetPlaySpaceLocalPos(pos),
                playerPlaySpaceForward = Boundary.Instance.GetPlaySpaceLocalDir(forward),
                playerPlaySpaceRotation = Boundary.Instance.GetPlaySpaceLocalRot(rot),

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

        public void SetEnvironmentParent(Transform t) {
            environmentParent = t;
        }
        public void SetNonAgentParents(Transform t) {
            if (rvo_non_agents.Count > 0) {
                foreach(RVO.NonAgent non_agent in rvo_non_agents) {
                    Debug.Log($"Set Environment for {non_agent.gameObject.name}");
                    non_agent.environment_parent = t;
                }
            }
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