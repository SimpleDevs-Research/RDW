using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using TMPro;

namespace RDW {
    public class SpatialManager : MonoBehaviour
    {
        public static SpatialManager Instance;
        public enum CalibrationHand { Left, Right }

        [Header("=== Tracked Anchors - SET THESE FIRST ===")]
        public Transform centerEyeCameraRef;
        public Transform leftHandAnchorRef;
        public Transform rightHandAnchorRef;
        public GameObject passthroughRef = null;
        [Space]
        public Transform calibrationEnvRef = null;
        public Transform headPosRef;
        public Transform boundaryRef;
        [Space]
        public Transform spatialAnchorPrefab;

        [Header("=== Calibration Setup ===")]
        [Tooltip("Prior to calibration, if there's any events you want to invoke, do so here")]
        public UnityEvent onPlaySpaceCalibrationStart;
        [Tooltip("All calibration steps to be run in order.")]
        public List<CalibrationStep> calibrationSteps;
        [Tooltip("Upon completing calibration, if there's any events you want to invoke, do so here")]
        public UnityEvent onPlaySpaceCalibrationEnd;

        [Header("=== Post-Calibration Inputs ===")]
        [Tooltip("OVRInput button for toggling passthrough")]
        public OVRInput.Button passthroughToggleInput = OVRInput.Button.Four;
        [Tooltip("OVRInput button for toggling calibration space objects")]
        public OVRInput.Button calibrationEnvToggleInput = OVRInput.Button.Three;
        [Tooltip("OVRInput button for restarting calibration")]
        public OVRInput.Button calibrationInput = OVRInput.Button.Two;
        [Tooltip("OVRInput button for moving beyond calibration")]
        public OVRInput.Button nextStepInput = OVRInput.Button.One;
        [Tooltip("Events for next step handling")]
        public UnityEvent onNextStepInput;

        [Header("=== Debugging ===")]
        public Transform debugRayIntersectionRef;
        public TextMeshPro debugTextbox;
        
        [Header("=== Cached Data - READ-ONLY ===")]
        [Tooltip("Are we calibrated already?")]
        public bool calibrated = false;
        [Tooltip("The minimum and maximum boundaries of your play area, defined during Room Scale Calibration")]
        public Vector3 minSpaceBound, maxSpaceBound;
        [Tooltip("The width of your play area")]
        public float spaceWidth = 10f;
        [Tooltip("The height of your play area")]
        public float spaceHeight = 10f;
        [Tooltip("Virtual world center")]
        public Vector3 worldCenter = Vector3.zero;

        private void Awake() {
            // create a new persistent instance
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        private void Start() {
            // Initialize this Singleton
            if (!calibrated) StartCoroutine(Calibrate());
        }

        private IEnumerator Calibrate() {
            // Initialize all cached data
            worldCenter = Vector3.zero;
            minSpaceBound = new Vector3(-5f, 0f, -5f);
            maxSpaceBound = new Vector3(5f, 0f, 5f);
            spaceWidth = 10f;
            spaceHeight = 10f;

            // If any events need to be called, do so here.
            onPlaySpaceCalibrationStart?.Invoke();

            // Run each calibration step in order
            calibrated = false;
            if (calibrationSteps.Count > 0) foreach(CalibrationStep step in calibrationSteps) {
                yield return step.Initialize();
            }
            calibrated = true;

            // If any events need to be called, do them here.
            onPlaySpaceCalibrationEnd?.Invoke();
        }

        public void TransitionToScene(string scene) { SceneManager.LoadScene(scene); }
        public void TransitionToScene(int scene) { SceneManager.LoadScene(scene); }

        // =======================
        // === DEBUG PURPOSES ===
        // =======================
        private void Update() {
            // if we haven't calibrated, then we don't do anything
            if (!calibrated) return;

            // We map button inputs to events
            if (OVRInput.GetDown(passthroughToggleInput)) TogglePassthrough();
            if (OVRInput.GetDown(calibrationEnvToggleInput)) ToggleCalibrationEnvironment();
            if (OVRInput.GetDown(calibrationInput)) StartCoroutine(Calibrate());
            if (OVRInput.GetDown(nextStepInput)) onNextStepInput?.Invoke();

            //if (debugTextbox != null) debugTextbox.text = $"{minSpaceBound}\n{maxSpaceBound}\n{calibrated}";
            if (debugRayIntersectionRef != null) {
                debugRayIntersectionRef.position = GetEdgePointFromRay(headPosRef.position, headPosRef.forward);
            }
        }

        public void TogglePassthrough(bool setTo) {
            if (passthroughRef != null) passthroughRef.SetActive(setTo);
        }
        public void TogglePassthrough() { 
            if (passthroughRef != null) TogglePassthrough(!passthroughRef.activeInHierarchy);
        }
        
        public void ToggleCalibrationEnvironment(bool setTo) {
            if (calibrationEnvRef != null) calibrationEnvRef.gameObject.SetActive(setTo);
        }
        public void ToggleCalibrationEnvironment() {
            if (calibrationEnvRef != null) ToggleCalibrationEnvironment(!calibrationEnvRef.gameObject.activeInHierarchy);
        }

        // ========================
        // === HELPER FUNCTIONS ===
        // =========================
        public Vector3 GetEdgePointFromRay(Vector3 start, Vector3 dir) {
            Vector3 direction = transform.InverseTransformDirection(Vector3.Normalize(dir.Flatten()));
            Vector3 origin = transform.InverseTransformPoint(start.Flatten());

            Vector3 invDir = new Vector3(1f/direction.x, 0f, 1f/direction.z);
            float t1 = (minSpaceBound.x - origin.x) * invDir.x;
            float t2 = (maxSpaceBound.x - origin.x) * invDir.x;
            float t3 = (minSpaceBound.z - origin.z) * invDir.z;
            float t4 = (maxSpaceBound.z - origin.z) * invDir.z;

            float tMin = Mathf.Max(Mathf.Min(t1, t2), Mathf.Min(t3, t4)); // Entry (we ignore this)
            float tMax = Mathf.Min(Mathf.Max(t1, t2), Mathf.Max(t3, t4)); // Exit

            return transform.TransformPoint(origin + direction * tMax);
        }
        public float GetMinDistanceToRectangleEdge() {
            return GetMinDistanceToRectangleEdge(headPosRef.position);
        }
        public float GetMinDistanceToRectangleEdge(Vector3 query) {
            Vector3 point = query.Flatten();
            float[] distances = new float[4];
            distances[0] = Mathf.Abs(point.x - minSpaceBound.x);
            distances[1] = Mathf.Abs(maxSpaceBound.x - point.x);
            distances[2] = Mathf.Abs(point.z - minSpaceBound.z);
            distances[3] = Mathf.Abs(maxSpaceBound.z - point.z);
            return Mathf.Min(distances);
        }
        public float GetDistanceAhead() { 
            Vector3 ahead = GetEdgePointFromRay(headPosRef.position, headPosRef.forward);
            return Vector3.Distance(ahead.Flatten(), headPosRef.position.Flatten()); 
        }
    }

}