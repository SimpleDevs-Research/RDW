using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    public class RDW : MonoBehaviour
    {
        public static RDW Instance;

        [Header("=== VR References ===")]
        public Transform centerEyeCamera;
        public Transform leftHandAnchor, rightHandAnchor;
        [Tooltip("It's expected that the eye tracking (insofar with Meta SDK) is captured as a Transform." +
                    "If using a Quest Pro, make sure to have the proper eye tracking setup done and" + 
                    "assign the Transform for your chosen eye here.")]
        public Transform eyeGaze;
        public Toggleable passthrough;

        [Header("=== RDW References ===")]
        public Transform headPoseAnchor;
        public Transform canvasTarget;
        public Transform playSpace;
        public Boundary boundary;

        [Header("=== Event Handling ===")]
        public UnityEvent onEnvironmentLoaded;
        public UnityEvent onEnvironmentUnloaded;

        [Header("=== Components ===")]
        public Calibrator calibrator;
        public Redirector2 redirector;

        [Header("=== RDW Data Cache ===")]
        public string id = "";
        public Vector3 worldCenter = Vector3.zero;
        public Vector3 minSpaceBound = new Vector3(-5f, 0f, -5f);
        public Vector3 maxSpaceBound = new Vector3(5f, 0f, 5f);
        public float spaceWidth = 10f;
        public float spaceDepth = 10f;
        public float minEdgeDistance = 0f;
        public GainSettings settings;

        [Header("=== Interactions ===")]
        public List<ButtonInput> buttonInteractions = new List<ButtonInput>();

        private void Awake() {
            Instance = this;
        }

        // Must be called if a login page is used and we want to set the user's ID.
        public void SetID(string id) {
            this.id = id;
        }

        // Technically redundant, but we have these as archival functions
        public void TogglePassthrough() { passthrough.Toggle(); }
        public void TogglePassthrough(bool t) { passthrough.Toggle(t); }

        // Called by Calibrator; used to set play space details
        public void ResetSpace() {
            worldCenter = Vector3.zero;
            minSpaceBound = new Vector3(-5f, 0f, -5f);
            maxSpaceBound = new Vector3(5f, 0f, 5f);
            spaceWidth = 10f;
            spaceDepth = 10f;
            playSpace.position = worldCenter;
            boundary.transform.localScale = new Vector3(10f, 5f, 10f);
            minEdgeDistance = GetMinDistanceToRectangleEdge(worldCenter);
        }
        public void SetSpace(Vector3 minBound, Vector3 maxBound) {
            // Determine the world center, width, and height from maxBound and minBound, which are epxected to be in world space
            worldCenter = (minBound + maxBound)/2f;
            spaceWidth = Mathf.Abs(maxBound.x - minBound.x);
            spaceDepth = Mathf.Abs(maxBound.z - minBound.z);
            
            // Now, let's reposition the play space & resize the boundary
            playSpace.position = worldCenter;
            boundary.transform.localScale = new Vector3(spaceWidth, 5f, spaceDepth);

            // Let's now calculate `minSpaceBound` and `maxSpaceBound` to be relative to play space
            minSpaceBound = playSpace.InverseTransformPoint(minBound);
            maxSpaceBound = playSpace.InverseTransformPoint(maxBound);
        }

        // Called by UI elements. If called, it'll attempt to call the function listed in the new setting's `sceneName`
        public void LoadEnvironment(GainSettings s) {
            // If we have a setting already loaded, then we must unload it.
            if (settings != null) {
                AdditiveSceneManager.Instance.UnloadScene(settings.sceneName);
            }
            // Set a new setting, and load the new scene.
            settings = s;
            AdditiveSceneManager.Instance.LoadScene(s.sceneName);
            redirector.Activate();
            onEnvironmentLoaded?.Invoke();
        }

        // Called by UI elements or by interactions. If called, it'll attempt to unload the current environment
        public void UnloadEnvironment() {
            // if no settings found, we ignore
            if (settings == null) {
                Debug.LogError("Cannot unload a nonexistent environment");
                return;
            }
            // Now try to unload via AdditiveSceneManager and reset settings to null
            AdditiveSceneManager.Instance.UnloadScene(settings.sceneName);
            settings = null;
            redirector.Deactivate();
            onEnvironmentUnloaded?.Invoke();
        }

        // `start` and `dir` are expected to be relative to world space
        public Vector3 GetEdgePointFromRay(Vector3 start, Vector3 dir, bool flatten = true) {
            if (flatten) {
                start = start.Flatten();
                dir = dir.Flatten();
            }
            // Normalize everything to the transform of the play space
            Vector3 direction = playSpace.InverseTransformDirection(Vector3.Normalize(dir));
            Vector3 origin = playSpace.InverseTransformPoint(start);

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
        public float GetMinDistanceToRectangleEdge(Vector3 query, bool flatten = true) {
            // Get the point relative to play space
            if (flatten) query = query.Flatten();
            Vector3 point = playSpace.InverseTransformPoint(query);
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

        public void GetClosestPointOnBoundary(Vector3 query, out Vector3 minPoint, out float minDistance, bool flatten = false) {
            // Calculate points
            if (flatten) query = query.Flatten();
            Vector3 north = GetEdgePointFromRay(query, Vector3.forward, flatten);
            Vector3 south = GetEdgePointFromRay(query, -Vector3.forward, flatten);
            Vector3 east = GetEdgePointFromRay(query, Vector3.right, flatten);
            Vector3 west = GetEdgePointFromRay(query, -Vector3.right, flatten);
            // Calculate distance to points
            // Default to north
            minDistance = Vector3.Distance(query, north);
            minPoint = north;
            // Calculate distances for each other direction
            float southDistance = Vector3.Distance(query, south);
            if (southDistance < minDistance) {
                minDistance = southDistance;
                minPoint = south;
            }
            float eastDistance = Vector3.Distance(query, east);
            if (eastDistance < minDistance) {
                minDistance = eastDistance;
                minPoint = east;
            }
            float westDistance = Vector3.Distance(query, west);
            if (westDistance < minDistance) {
                minDistance = westDistance;
                minPoint = west;
            }
            // Since we're using `out`, we're done here
        }
    }
}
