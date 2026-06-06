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
        public OVRInput.Button calibrationFinishedInput;

        [Header("=== Settings ===")]
        [SerializeField] private float triggerThreshold = 0.75f;
        [SerializeField] private LayerMask raycastLayers;

        private bool tracking = false;
        private GameObject raycastTarget; 

        // Overriding the base `Calibrate` for our own head calibration.
        public override IEnumerator Calibrate() { 
            // Set our calibration status to `false`
            _calibrated = false;

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
                ? RDW.Instance.leftHandAnchor 
                : RDW.Instance.rightHandAnchor;

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
                    Calibrator.Instance.minSpaceAnchor.position = hit.point;
                }
                // Case 2: we're not holding down the trigger & we're still tracking 
                if (!triggering && tracking) {
                    // Stop tracking
                    EndTracking();
                }


                // Terminate if we're finished
                if (OVRInput.GetDown(calibrationFinishedInput)) _calibrated = true;
                
                // Make sure the update loop moves to the next frame
                yield return null;
            }

            // Upon completion, we must destroy the raycast cursor
            Destroy(raycastTarget);
        }

        private void StartTracking(Vector3 startPoint) {
            // Initialize the min and max anchors of the calibration space
            Calibrator.Instance.maxSpaceAnchor.position = startPoint;
            Calibrator.Instance.minSpaceAnchor.position = startPoint;
            
            // Tracking Check Flag
            tracking = true;
        }

        private void EndTracking() {
            // We must update RDW with these details
            RDW.Instance.SetSpace(Calibrator.Instance.minSpaceAnchor.position, Calibrator.Instance.maxSpaceAnchor.position);

            // Update check flag
            tracking = false;
        }
    }
}