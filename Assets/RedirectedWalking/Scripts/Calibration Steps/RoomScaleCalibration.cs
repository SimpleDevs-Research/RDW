using System;
using System.Collections;
using UnityEngine;

namespace RDW {
    [System.Serializable]
    public class RoomScaleCalibration : CalibrationStep
    {
        [Header("=== References ===")]
        public OVRInput.Controller pointerController = OVRInput.Controller.RTouch;
        public OVRInput.Axis1D calibrationTriggerInput = OVRInput.Axis1D.PrimaryIndexTrigger;
        public GameObject raycastTargetPrefab = null;
        [Space]
        public OVRInput.Button calibrationFinishedInput;
        public LineRenderer lineRenderer;

        [Header("=== Settings ===")]
        [SerializeField] private float triggerThreshold = 0.75f;
        [SerializeField] private LayerMask raycastLayers;

        private bool tracking = false;
        private GameObject raycastTarget; 
        private Transform[] spatialAnchors;

        // Overriding the base `Calibrate` for our own head calibration.
        public override IEnumerator Calibrate() { 
            // We cannot proceed with a spatial anchor prefab is not set.
            if (SpatialManager.Instance.spatialAnchorPrefab == null || SpatialManager.Instance.calibrationEnvRef == null) {
                Debug.Log("Cannot calibrate room space because of missing spatial anchor prefab or calibration environment reference.");
                yield break;
            }

            // Initialize our anchors
            spatialAnchors = new Transform[2];
            spatialAnchors[0] = Instantiate(SpatialManager.Instance.spatialAnchorPrefab, Vector3.zero, Quaternion.identity) as Transform;
            spatialAnchors[0].parent = SpatialManager.Instance.calibrationEnvRef;
            spatialAnchors[0].gameObject.SetActive(false);
            spatialAnchors[1] = Instantiate(SpatialManager.Instance.spatialAnchorPrefab, Vector3.zero, Quaternion.identity) as Transform;
            spatialAnchors[1].parent = SpatialManager.Instance.calibrationEnvRef;
            spatialAnchors[1].gameObject.SetActive(true);

            // We will instantiate a raycast target indicator. 
            // Make sure it doesn't have a collider involved.
            // Also instantiate a RaycastHit hit
            if (raycastTargetPrefab != null) {
                raycastTarget = Instantiate(raycastTargetPrefab, Vector3.zero, Quaternion.identity);
            } else {
                raycastTarget = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                raycastTarget.transform.localScale = Vector3.one * 0.1f;
                Destroy(raycastTarget.GetComponent<Collider>());
            }
            raycastTarget.SetActive(false);
            RaycastHit hit;

            // Initialize a reference to the hand being used for pointing
            Transform pointer = (pointerController == OVRInput.Controller.LTouch) 
                ? SpatialManager.Instance.leftHandAnchorRef 
                : SpatialManager.Instance.rightHandAnchorRef;

            // If we have a line renderer, we initialize it
            if (lineRenderer != null) lineRenderer.positionCount = 2;

            // While we're not calibrated, we will loop
            while(!_calibrated) {
                
                // Update values: the trigger's 1D Axis val + if we're hitting the ground
                bool triggering = OVRInput.Get(calibrationTriggerInput, pointerController) > triggerThreshold;
                bool hitting = Physics.Raycast(pointer.position, pointer.forward, out hit, 200f, raycastLayers);

                // 2 distinct states: we're either holding the trigger or not.
                // Case 0: we're hitting, so we update the raycast target position
                if (hitting) {
                    raycastTarget.SetActive(true);
                    raycastTarget.transform.position = hit.point;
                }
                else {
                    raycastTarget.SetActive(false);
                }
                // Case 1: we're holding down the trigger and we're hitting the floor
                if (triggering && hitting) {
                    // Handle the starting of tracking (if we haven't yet)
                    if (!tracking) StartTracking(hit.point);
                    // Update the 2nd spatial anchor and cursor
                    spatialAnchors[1].position = hit.point;
                }
                // Case 2: we're not holding down the trigger & we're still tracking 
                if (!triggering && tracking) {
                    // Stop tracking
                    EndTracking();
                }
                // Case 3: We are tracking, and we have a line renderer to represent this tracking space
                if (tracking && lineRenderer != null) {
                    lineRenderer.SetPosition(0, spatialAnchors[0].position);
                    lineRenderer.SetPosition(1, spatialAnchors[1].position);
                }

                // Terminate if we're finished
                if (OVRInput.GetDown(calibrationFinishedInput)) _calibrated = true;
                
                // Make sure the update loop moves to the next frame
                yield return null;
            }

            // Upon completion, we must destroy the spatial anchors, raycast cursor, and floor
            Destroy(spatialAnchors[1]);
            Destroy(spatialAnchors[0]);
            Destroy(raycastTarget);
        }

        private void StartTracking(Vector3 startPoint) {
            // Initialize the min and max anchors of the calibration space
            spatialAnchors[0].gameObject.SetActive(true);
            spatialAnchors[0].position = startPoint;
            spatialAnchors[1].gameObject.SetActive(false);
            spatialAnchors[1].position = startPoint;
            
            // Tracking Check Flag
            tracking = true;
        }

        private void EndTracking() {
            // Update our 2nd spatial anchor to be visible
            spatialAnchors[1].gameObject.SetActive(true);

            // Our spatial anchors hold the details of our boundary limits.
            // We must updte SpatialManager with these details
            SpatialManager.Instance.maxSpaceBound = spatialAnchors[0].position;
            SpatialManager.Instance.minSpaceBound = spatialAnchors[1].position;
            SpatialManager.Instance.worldCenter = (spatialAnchors[0].position + spatialAnchors[1].position)/2f;
            SpatialManager.Instance.spaceWidth = Mathf.Abs(spatialAnchors[0].position.x - spatialAnchors[1].position.x);
            SpatialManager.Instance.spaceHeight= Mathf.Abs(spatialAnchors[0].position.z - spatialAnchors[1].position.z);
            
            // We must set the boundary, if set, to match the scale defined here
            if (SpatialManager.Instance.boundaryRef != null) {
                SpatialManager.Instance.boundaryRef.position = SpatialManager.Instance.worldCenter;
                SpatialManager.Instance.boundaryRef.localScale = new Vector3(
                    SpatialManager.Instance.spaceWidth, 
                    1f, 
                    SpatialManager.Instance.spaceHeight
                );
                SpatialManager.Instance.boundaryRef.GetComponent<BoundaryProximity>()?.SetPlayer(SpatialManager.Instance.headPosRef);
            }

            // Update check flag
            tracking = false;
        }
    }
}