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
        public Transform headRef;
        public Transform leftHandRef;
        public Transform rightHandRef;
        public Transform headPosPrefab;
        public Transform spatialAnchorPrefab;

        [Header("=== Head Calibration ===")]
        public bool calibrateHeadOnAwake = true;
        public Transform headPosRef;
        public UnityEvent onHeadCalibrated;

        [Header("=== Spatial Calibration ===")]
        public bool calibrateSpaceOnAwake = true;
        public CalibrationHand calibrationHand = CalibrationHand.Right;
        // These can be null, or you can define them manually
        [Tooltip("Game Objects representing the position of anchors. Can be set manually; if unset, the system will auto-replace them.")]
        public Transform minAnchorRef, maxAnchorRef;
        public UnityEvent onPlaySpaceCalibrated;

        [Header("=== Debugging ===")]
        public Transform debugRayIntersectionRef;
        public TextMeshPro debugTextbox;
        public bool visibleAnchors = false;

        [Header("=== Spatial Anchoring - Flags (Read-Only) ===")]
        [Tooltip("Are we calibrated already?")]
        public bool calibrated = false;
        public bool calibrating = false;
        
        [Header("=== Cacbed Data ===")]
        [Tooltip("Local displacement of the head pos ref from the head ref")]
        public Vector3 headPosDisp;
        [Tooltip("Local-scale min and max anchors")]
        public Vector3 localAnchorMin, localAnchorMax;

        private void Start() {
            // If an instance of this already exists, then this shouldn't do anything
            if (Instance != null) {
                // Transfer the game object knowledge from this guy to the instance
                // Tracked Anchors
                Instance.headRef = this.headRef;
                Instance.leftHandRef = this.leftHandRef;
                Instance.rightHandRef = this.rightHandRef;
                Instance.headPosPrefab = this.headPosPrefab;
                Instance.spatialAnchorPrefab = this.spatialAnchorPrefab;
                // Head Calibration
                Instance.headPosRef = this.headPosRef;
                Instance.calibrateHeadOnAwake = this.calibrateHeadOnAwake;
                Instance.onHeadCalibrated = this.onHeadCalibrated;
                // Spatial Calibration
                Instance.minAnchorRef = this.minAnchorRef;
                Instance.maxAnchorRef = this.maxAnchorRef;
                Instance.onPlaySpaceCalibrated = this.onPlaySpaceCalibrated;
                // Debugging
                Instance.debugRayIntersectionRef = this.debugRayIntersectionRef;
                Instance.debugTextbox = this.debugTextbox;
                // Invoke initialize on the incoming instance
                Instance.Initialize();
                // Destroy this version of the instance to make way for the incoming instance.
                Destroy(gameObject);
                return;
            }
            // Otherwise, create a new persistent instance
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Initialize this Singleton
            Initialize();
        }

        private void Initialize() {
            // First, calibrate the head if necessary
            if (calibrateHeadOnAwake)   CalibrateHeadPos();
            else                        InitializeHeadPos();

            // Second, calibrate the playspace anchors.
            // The player must dictate TWO spatial anchors - a min and a max. 
            //      The point here is to define localAnchorMin and localAnchorMax.
            //      if we pulled this singleton from another scene (where these were likely to be set) then we don't need to set these.
            if (!calibrated)    CalibrateSpace();
            else                InitializeSpace();

            // Debug stuff
            if (debugRayIntersectionRef == null) {
                debugRayIntersectionRef = Instantiate(spatialAnchorPrefab, Vector3.zero, Quaternion.identity) as Transform;
                debugRayIntersectionRef.localScale = Vector3.one * 0.15f;
            }

            // Toggle visibility
            ToggleVisible(visibleAnchors);
        }

        // =================================
        // === HEAD POSITION CALIBRATION ===
        // =================================
        public void CalibrateHeadPos() {
            // Assume that the user's hands and head exists. If not, then we cannot do anything here.
            if (leftHandRef == null || rightHandRef == null || headRef == null) {
                Debug.Log("Cannot estimate true head displacement because of missing hand refs or head ref.");
                return;
            }

            // Get the local positions of both hands
            Vector3 left_localPos = headRef.InverseTransformPoint(leftHandRef.position);
            Vector3 right_localPos = headRef.InverseTransformPoint(rightHandRef.position);

            // Calculate the Z position of both left and right (via averaging)
            headPosDisp = new Vector3(0f, 0f, (left_localPos.z + right_localPos.z)/2f);
            
            // Reposition the head
            InitializeHeadPos();
        }
        public void InitializeHeadPos() {
            if (headPosRef == null) headPosRef  = Instantiate(headPosPrefab, Vector3.zero, Quaternion.identity) as Transform;
            headPosRef.parent = headRef;
            headPosRef.localPosition = headPosDisp;
            headPosRef.localRotation = Quaternion.identity;
            headPosRef.localScale = Vector3.one * 0.025f;
            onHeadCalibrated?.Invoke();
        }

        // =============================
        // === PLAYSPACE CALBIRATION ===
        // =============================
        public void CalibrateSpace() { 
            StartCoroutine(CalibrateSpaceUpdate()); 
        }
        private IEnumerator CalibrateSpaceUpdate() {
            // Initialize calibratin flag
            calibrating = true;

            // These are local only to the function and are used to track changes to our placed anchors during the while loop.
            int anchor_count = 0;

            // For each raycast target, we instantate them from primitives. 
            //      These will represent the place in the floor the hands raycast to. 
            //      They'll be deleted at the end of this function.
            GameObject raycast_target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            raycast_target.transform.localScale = Vector3.one * 0.1f;
            Destroy(raycast_target.GetComponent<SphereCollider>());

            // Similarly, we only create the floor for this scene
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.position = Vector3.zero;
            floor.transform.rotation = Quaternion.identity;
            floor.transform.localScale = new Vector3(10f, 10f, 10f);
            floor.GetComponent<Renderer>().enabled = false;
            
            // Initialize some primers
            Transform hand = (calibrationHand == CalibrationHand.Left) ? leftHandRef : rightHandRef;
            OVRInput.RawButton inputButton = (calibrationHand == CalibrationHand.Left) ? OVRInput.RawButton.LIndexTrigger : OVRInput.RawButton.RIndexTrigger;
            Vector3[] hits = new Vector3[2];

            // Initialize calibration routine
            while(calibrating) {
                // for the preferred calibration hand, do a raycast from the hand forward to the ground.
                RaycastHit hit;
                if (Physics.Raycast(hand.position, hand.forward, out hit, 200f)) {
                    // Set indicator
                    raycast_target.transform.position = hit.point;
                    // If detect index trigger, press, then place an anchor.
                    if (OVRInput.GetUp(inputButton) && anchor_count < 2) {
                        // Instantiate new anchor
                        hits[anchor_count] = transform.InverseTransformPoint(hit.point);
                        anchor_count += 1;
                    }
                }
                // If our anchor count is 2, then we've placed our anchors
                calibrating = anchor_count < 2;
                yield return null;
            }
            // Destroy our left and right hand targets
            Destroy(raycast_target);
            Destroy(floor);

            // Assign min and max local anchors, and calibrated status
            localAnchorMin = hits[0];
            localAnchorMax = hits[1];
            calibrated = true;

            // Call `InitializeSpace()` upon completion
            InitializeSpace();
        }
        public void InitializeSpace() {
            // Create the spatial anchors if needed
            if (minAnchorRef == null) {
                minAnchorRef = Instantiate(spatialAnchorPrefab, Vector3.zero, Quaternion.identity) as Transform;
                minAnchorRef.localScale = Vector3.one * 0.1f;
            }
            if (maxAnchorRef == null) {
                maxAnchorRef = Instantiate(spatialAnchorPrefab, Vector3.zero, Quaternion.identity) as Transform;
                maxAnchorRef.localScale = Vector3.one * 0.1f;
            }

            // Place min and max anchors
            minAnchorRef.position = transform.TransformPoint(localAnchorMin);
            maxAnchorRef.position = transform.TransformPoint(localAnchorMax);

            // If any events need to be called, do them here.
            onPlaySpaceCalibrated?.Invoke();
        }

        // =======================
        // === DEBUG PURPOSES ===
        // =======================
        private void Update() {
            if (debugTextbox != null) debugTextbox.text = $"{localAnchorMin}\n{localAnchorMax}\n{calibrated}";
            if (!calibrated) return;
            debugRayIntersectionRef.position = GetEdgePointFromRay(headPosRef.position, headPosRef.forward);
        }

        // ========================
        // === HELPER FUNCTIONS ===
        // =========================
        public Vector3 GetEdgePointFromRay(Vector3 start, Vector3 dir) {
            Vector3 direction = transform.InverseTransformDirection(Vector3.Normalize(dir.Flatten()));
            Vector3 origin = transform.InverseTransformPoint(start.Flatten());

            Vector3 invDir = new Vector3(1f/direction.x, 0f, 1f/direction.z);
            float t1 = (localAnchorMin.x - origin.x) * invDir.x;
            float t2 = (localAnchorMax.x - origin.x) * invDir.x;
            float t3 = (localAnchorMin.z - origin.z) * invDir.z;
            float t4 = (localAnchorMax.z - origin.z) * invDir.z;

            float tMin = Mathf.Max(Mathf.Min(t1, t2), Mathf.Min(t3, t4)); // Entry (we ignore this)
            float tMax = Mathf.Min(Mathf.Max(t1, t2), Mathf.Max(t3, t4)); // Exit

            return transform.TransformPoint(origin + direction * tMax);
        }
        public float GetMinDistanceToRectangleEdge() {
            return GetMinDistanceToRectangleEdge(headPosRef.position);
        }
        public float GetMinDistanceToRectangleEdge(Vector3 query) {
            Vector3 point = transform.InverseTransformPoint(query.Flatten());
            float[] distances = new float[4];
            distances[0] = Mathf.Abs(point.x - localAnchorMin.x);
            distances[1] = Mathf.Abs(localAnchorMax.x - point.x);
            distances[2] = Mathf.Abs(point.z - localAnchorMin.z);
            distances[3] = Mathf.Abs(localAnchorMax.z - point.z);
            return Mathf.Min(distances);
        }
        public float GetDistanceAhead() { 
            Vector3 ahead = GetEdgePointFromRay(headPosRef.position, headPosRef.forward);
            return Vector3.Distance(ahead.Flatten(), headPosRef.position.Flatten()); 
        }

        public void ToggleVisible() {
            visibleAnchors = !visibleAnchors;
            ToggleVisible(visibleAnchors);
        }
        public void ToggleVisible(bool setTo) {
            headPosRef?.gameObject.SetActive(setTo);
            minAnchorRef?.gameObject.SetActive(setTo);
            maxAnchorRef?.gameObject.SetActive(setTo);
            debugRayIntersectionRef?.gameObject.SetActive(setTo);
        }

        public void TransitionToScene(string scene) { SceneManager.LoadScene(scene); }
        public void TransitionToScene(int scene) { SceneManager.LoadScene(scene); }
    }

}