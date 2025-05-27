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

        [Header("=== Tracked Anchors - SET THESE FIRST ===")]
        public Transform headRef;
        public Transform leftHandRef;
        public Transform rightHandRef;
        public Transform headPosPrefab;
        public Transform spatialAnchorPrefab;

        [Header("=== Head Calibration ===")]
        public Transform headPosRef;
        public bool calibrateHeadOnAwake = true;
        public UnityEvent onHeadCalibrated;

        [Header("=== Spatial Calibration ===")]
        // These can be null, or you can define them manually
        [Tooltip("Game Objects representing the position of anchors and floor. Can be set manually; if unset, the system will auto-replace them.")]
        public Transform minAnchorRef, maxAnchorRef, floorRef;
        public UnityEvent onPlaySpaceCalibrated;

        [Header("=== Debugging ===")]
        public Transform debugRayIntersectionRef;
        public TextMeshPro debugTextbox;

        [Header("=== Spatial Anchoring - Flags (Read-Only) ===")]
        [Tooltip("Are we calibrated already?")]
        public bool calibrated = false;
        public bool calibrating = false;
        
        [Header("=== Cacbed Data ===")]
        [Tooltip("Local displacement of the head pos ref from the head ref")]
        public Vector3 headPosDisp;
        [Tooltip("Local-scale min and max anchors")]
        public Vector3 localAnchorMin, localAnchorMax;

        private void Awake() {
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
                Instance.floorRef = this.floorRef;
                Instance.onPlaySpaceCalibrated = this.onPlaySpaceCalibrated;
                // Debugging
                Instance.debugRayIntersectionRef = this.debugRayIntersectionRef;
                Instance.debugTextbox = this.debugTextbox;
                // Destroy this version of the instance to make way for the incoming instance.
                Destroy(gameObject);
                // Invoke initialize on the incoming instance
                Instance.Initialize();
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
            // Create the head pos ref if not defined
            if (headPosRef == null) {
                headPosRef  = Instantiate(headPosPrefab, Vector3.zero, Quaternion.identity) as Transform;
                headPosRef.parent = headRef;
                headPosRef.localPosition = Vector3.zero;
                headPosRef.localRotation = Quaternion.identity;
                headPosRef.localScale = Vector3.one * 0.05f;
            }
            // Calibrate the head.
            if (calibrateHeadOnAwake) CalibrateHeadPos();
            else {
                headPosRef.localPosition = headPosDisp;
                onHeadCalibrated?.Invoke();
            }

            // Second, calibrate the playspace anchors.
            // Create the floor and spatial anchors if needed
            if (floorRef == null) {
                GameObject floorGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floorRef = floorGo.transform;
                floorRef.rotation = Quaternion.Euler(90f, 0f, 0f);
                floorRef.localScale = new Vector3(10f, 10f, 1f);
                floorRef.GetComponent<Renderer>().enabled = false;
            }
            // Place min and max anchors
            if (minAnchorRef == null) {
                minAnchorRef = Instantiate(spatialAnchorPrefab, Vector3.zero, Quaternion.identity) as Transform;
                minAnchorRef.localScale = Vector3.one * 0.1f;
            }
            if (maxAnchorRef == null) {
                maxAnchorRef = Instantiate(spatialAnchorPrefab, Vector3.zero, Quaternion.identity) as Transform;
                maxAnchorRef.localScale = Vector3.one * 0.1f;
            }
            // The player must dictate TWO spatial anchors - a min and a max. 
            //      The point here is to define localAnchorMin and localAnchorMax.
            //      if we pulled this singleton from another scene (where these were likely to be set) then we don't need to set these.
            if (!calibrated) StartCoroutine(CalibrateSpace());
            else {
                minAnchorRef.position = transform.TransformPoint(localAnchorMin);
                maxAnchorRef.position = transform.TransformPoint(localAnchorMax);
                onPlaySpaceCalibrated?.Invoke();
            }

            // Debug stuff
            if (debugRayIntersectionRef == null) {
                debugRayIntersectionRef = Instantiate(spatialAnchorPrefab, Vector3.zero, Quaternion.identity) as Transform;
                debugRayIntersectionRef.localScale = Vector3.one * 0.15f;
            }
        }

        public void CalibrateHeadPos() {
            // Assume that the user's hands exists. If not, then we cannot do anything here.
            if (leftHandRef == null || rightHandRef == null || headPosRef == null) {
                Debug.Log("Cannot estimate true head displacement because of missing hand refs or head pose ref.");
                return;
            }
            // Get the local positions of both hands
            Vector3 left_localPos = headRef.InverseTransformPoint(leftHandRef.position);
            Vector3 right_localPos = headRef.InverseTransformPoint(rightHandRef.position);
            // Calculate the Z position of both left and right (via averaging)
            headPosDisp = new Vector3(0f, 0f, (left_localPos.z + right_localPos.z)/2f);
            headPosRef.localPosition = headPosDisp;
            // Call events if needed
            onHeadCalibrated?.Invoke();
        }

        private IEnumerator CalibrateSpace() {
            // Initialize calibratin flag
            calibrating = true;

            // These are local only to the function and are used to track changes to our placed anchors during the while loop.
            int anchor_count = 0;

            // For each raycast target, we instantate them from primitives. 
            //      These will represent the place in the floor the hands raycast to. 
            //      They'll be deleted at the end of this function.
            GameObject left_hand_raycast_target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            left_hand_raycast_target.transform.localScale = Vector3.one * 0.1f;
            Destroy(left_hand_raycast_target.GetComponent<SphereCollider>());
            GameObject right_hand_raycast_target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            right_hand_raycast_target.transform.localScale = Vector3.one * 0.1f;
            Destroy(right_hand_raycast_target.GetComponent<SphereCollider>());
            
            // Initialize calibration routine
            while(calibrating) {
                // for each hand, do a raycast from the hand forward to the ground.
                RaycastHit hit;
                if (Physics.Raycast(leftHandRef.position, leftHandRef.forward, out hit, 100f)) {
                    // Set indicator
                    left_hand_raycast_target.transform.position = hit.point;
                    // If detect index trigger, press, then place an anchor.
                    if (OVRInput.GetUp(OVRInput.RawButton.LIndexTrigger) && anchor_count < 2) {
                        // Instantiate new anchor
                        if (anchor_count == 0)      minAnchorRef.position = hit.point;
                        else if (anchor_count == 1) maxAnchorRef.position = hit.point;
                        anchor_count += 1;
                    }
                }
                if (Physics.Raycast(rightHandRef.position, rightHandRef.forward, out hit, 100f)) {
                    // Set indicator
                    right_hand_raycast_target.transform.position = hit.point;
                    // If detect index trigger, press, then place an anchor.
                    if (OVRInput.GetUp(OVRInput.RawButton.RIndexTrigger) && anchor_count < 2) {
                        // Instantiate new anchor
                        if (anchor_count == 0)      minAnchorRef.position = hit.point;
                        else if (anchor_count == 1) maxAnchorRef.position = hit.point;
                        anchor_count += 1;
                    }
                }
                // If our anchor count is 2, then we've placed our anchors
                calibrating = anchor_count < 2;
                yield return null;
            }
            // Destroy our left and right hand targets
            Destroy(left_hand_raycast_target);
            Destroy(right_hand_raycast_target);
            // Assign min and max local anchors
            localAnchorMin = transform.InverseTransformPoint(minAnchorRef.position);
            localAnchorMax = transform.InverseTransformPoint(maxAnchorRef.position);
            // set Defined status
            calibrated = true;

            // If any events need to be called, do them here.
            onPlaySpaceCalibrated?.Invoke();
        }

        // PURELY FOR DEBUG PURPOSES
        private void Update() {
            if (debugTextbox != null) debugTextbox.text = $"{localAnchorMin}\n{localAnchorMax}\n{calibrated}";
            if (!calibrated) return;
            debugRayIntersectionRef.position = GetEdgePointFromRay(headPosRef.position, headPosRef.forward);
        }

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

            // Calculate the orthogonal distances
            float[] distances = new float[4];
            distances[0] = Mathf.Abs(point.x - localAnchorMin.x);
            distances[1] = Mathf.Abs(localAnchorMax.x - point.x);
            distances[2] = Mathf.Abs(point.z - localAnchorMin.z);
            distances[3] = Mathf.Abs(localAnchorMax.z - point.z);
            return Mathf.Min(distances);
        }

        public void ToggleVisible(bool setTo) {
            headPosRef?.gameObject.SetActive(setTo);
            minAnchorRef?.gameObject.SetActive(setTo);
            maxAnchorRef?.gameObject.SetActive(setTo);
            floorRef?.gameObject.SetActive(setTo);
            debugRayIntersectionRef?.gameObject.SetActive(setTo);
        }

        public void TransitionToScene(string scene) { SceneManager.LoadScene(scene); }
        public void TransitionToScene(int scene) { SceneManager.LoadScene(scene); }
        public float GetDistanceAhead() { return Vector3.Distance(debugRayIntersectionRef.position.Flatten(), headPosRef.position.Flatten()); }
    }

}