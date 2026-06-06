using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    [System.Serializable]
    public class ButtonInput {
        public string name;
        public OVRInput.Button input;
        public bool enabled;
        public UnityEvent events;
    }

    public class RDW : MonoBehaviour
    {
        public static RDW Instance;

        [Header("=== VR References ===")]
        public Transform centerEyeCamera;
        public Transform leftHandAnchor, rightHandAnchor;
        public Transform eyeGaze;
        public Transform canvasTarget;
        public OVRPassthroughLayer passthrough;

        [Header("=== RDW References ===")]
        public Transform headPoseAnchor;
        public Transform playSpace;
        public Transform boundary;
        public Canvas canvas;

        [Header("=== Components ===")]
        public Calibrator calibrator;

        [Header("=== RDW Data Cache ===")]
        public Vector3 worldCenter = Vector3.zero;
        public Vector3 minSpaceBound = new Vector3(-5f, 0f, -5f);
        public Vector3 maxSpaceBound = new Vector3(5f, 0f, 5f);
        public float spaceWidth = 10f;
        public float spaceDepth = 10f;

        [Header("=== Interactions ===")]
        public List<ButtonInput> buttonInteractions = new List<ButtonInput>();

        private void Awake() {
            Instance = this;
        }

        private void OnEnable() {
            canvas.gameObject.SetActive(true);
        }

        private void Update() {
            foreach(ButtonInput bi in buttonInteractions) 
                if (bi.enabled && OVRInput.GetDown(bi.input)) 
                    bi.events?.Invoke();
        }

        private void OnDisable() {
            canvas.gameObject.SetActive(false);
        }

        public void TogglePassthrough() { passthrough.enabled = !passthrough.enabled; }
        public void TogglePassthrough(bool t) { passthrough.enabled = t; }

        public void ResetSpace() {
            worldCenter = Vector3.zero;
            minSpaceBound = new Vector3(-5f, 0f, -5f);
            maxSpaceBound = new Vector3(5f, 0f, 5f);
            spaceWidth = 10f;
            spaceDepth = 10f;
            playSpace.position = worldCenter;
            boundary.position = worldCenter;
            boundary.localScale = new Vector3(10f, 1f, 10f);
        }

        public void SetSpace(Vector3 minBound, Vector3 maxBound) {
            // Determine the world center, width, and height from maxBound and minBound, which are epxected to be in world space
            worldCenter = (minBound + maxBound)/2f;
            spaceWidth = Mathf.Abs(maxBound.x - minBound.x);
            spaceDepth = Mathf.Abs(maxBound.z - minBound.z);
            
            // Now, let's reposition the play space & resize the boundary
            playSpace.position = worldCenter;
            boundary.localScale = new Vector3(spaceWidth, 1f, spaceDepth);
            boundary.GetComponent<BoundaryProximity>()?.SetPlayer(headPoseAnchor);

            // Let's now calculate `minSpaceBound` and `maxSpaceBound` to be relative to play space
            minSpaceBound = playSpace.InverseTransformPoint(minBound);
            maxSpaceBound = playSpace.InverseTransformPoint(maxBound);
        }

        // `start` and `dir` are expected to be relative to world space
        public Vector3 GetEdgePointFromRay(Vector3 start, Vector3 dir) {
            // Normalize everything to the transform of the play space
            Vector3 direction = playSpace.InverseTransformDirection(Vector3.Normalize(dir.Flatten()));
            Vector3 origin = playSpace.InverseTransformPoint(start.Flatten());

            Vector3 invDir = new Vector3(1f/direction.x, 0f, 1f/direction.z);
            float t1 = (minSpaceBound.x - origin.x) * invDir.x;
            float t2 = (maxSpaceBound.x - origin.x) * invDir.x;
            float t3 = (minSpaceBound.z - origin.z) * invDir.z;
            float t4 = (maxSpaceBound.z - origin.z) * invDir.z;

            float tMin = Mathf.Max(Mathf.Min(t1, t2), Mathf.Min(t3, t4)); // Entry (we ignore this)
            float tMax = Mathf.Min(Mathf.Max(t1, t2), Mathf.Max(t3, t4)); // Exit

            // Return in world space coordinates
            return playSpace.TransformPoint(origin + direction * tMax);
        }
        public Vector3 GetEdgePointFromRay() { 
            return GetEdgePointFromRay(headPoseAnchor.position, headPoseAnchor.forward); 
        }

        public float GetMinDistanceToRectangleEdge() {
            return GetMinDistanceToRectangleEdge(headPoseAnchor.position);
        }
        public float GetMinDistanceToRectangleEdge(Vector3 query) {
            // Get the point relative to play space
            Vector3 point = playSpace.InverseTransformPoint(query.Flatten());
            float[] distances = new float[4];
            distances[0] = Mathf.Abs(point.x - minSpaceBound.x);
            distances[1] = Mathf.Abs(maxSpaceBound.x - point.x);
            distances[2] = Mathf.Abs(point.z - minSpaceBound.z);
            distances[3] = Mathf.Abs(maxSpaceBound.z - point.z);
            return Mathf.Min(distances);
        }
        public float GetDistanceAhead() { 
            Vector3 ahead = GetEdgePointFromRay();
            return Vector3.Distance(ahead.Flatten(), headPoseAnchor.position.Flatten()); 
        }
    }
}
