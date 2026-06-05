using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace RDW {
    [System.Serializable]
    public class HeadCalibration : CalibrationStep
    {   
        [Header("=== References ===")]
        public TextMeshProUGUI offsetTextboxRef = null;
        public OVRInput.Button calibrateHeadInput;
        public OVRInput.Button calibrationFinishedInput;

        [Header("=== Step-Specific Event Handling ===")]
        public UnityEvent onHeadPositionSet;

        [Header("=== Data Cache - READ-ONLY ===")]
        public Vector3 headDisplacement = Vector3.zero;

        // Overriding the base `Calibrate` for our own head calibration.
        public override IEnumerator Calibrate() { 
            // Assume that the user's hands and head exists. If not, then we cannot do anything here.
            if (
                SpatialManager.Instance.centerEyeCameraRef == null 
                || SpatialManager.Instance.leftHandAnchorRef == null 
                || SpatialManager.Instance.rightHandAnchorRef == null 
                || SpatialManager.Instance.headPosRef == null
            ) {
                Debug.Log("Cannot estimate true head displacement because of missing hand refs or head refs.");
                yield break;
            }

            // While we're not calibrated, we will loop
            while(!_calibrated) {
                // Only invoke the calibration step if the user clicked the `calibrateHeadInput` button
                if (OVRInput.GetDown(calibrateHeadInput)) CalibrateHead();
                // Terminate if we're finished
                if (OVRInput.GetDown(calibrationFinishedInput)) _calibrated = true;
                // Fill the offset textbox if set
                if (offsetTextboxRef != null) offsetTextboxRef.text = SpatialManager.Instance.headPosRef.localPosition.z.ToString();
                // Make sure the update loop moves to the next frame
                yield return null;
            }
        }

        private void CalibrateHead() {
            // Get the local positions of both hands
            Vector3 leftLocalPos = SpatialManager.Instance.centerEyeCameraRef.InverseTransformPoint(SpatialManager.Instance.leftHandAnchorRef.position);
            Vector3 rightLocalPos = SpatialManager.Instance.centerEyeCameraRef.InverseTransformPoint(SpatialManager.Instance.rightHandAnchorRef.position);

            // Calculate the Z position of both left and right (via averaging). Then set the local position
            SpatialManager.Instance.headPosRef.localPosition = new Vector3(0f, 0f, (leftLocalPos.z + rightLocalPos.z)/2f);

            // Debug Log
            Debug.Log("Head Calibrated!");

            // Invoke any events
            onHeadPositionSet?.Invoke();
        }
    }
}