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
            // Set our calibration status to `false`
            _calibrated = false;

            // Assume that the user's hands and head exists. If not, then we cannot do anything here.
            if (
                RDW.Instance.centerEyeCamera == null 
                || RDW.Instance.leftHandAnchor == null 
                || RDW.Instance.rightHandAnchor == null 
                || RDW.Instance.headPoseAnchor == null
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
                if (offsetTextboxRef != null) offsetTextboxRef.text = RDW.Instance.headPoseAnchor.localPosition.z.ToString();
                // Make sure the update loop moves to the next frame
                yield return null;
            }
        }

        private void CalibrateHead() {
            // Get the local positions of both hands
            Vector3 leftLocalPos = RDW.Instance.centerEyeCamera.InverseTransformPoint(RDW.Instance.leftHandAnchor.position);
            Vector3 rightLocalPos = RDW.Instance.centerEyeCamera.InverseTransformPoint(RDW.Instance.rightHandAnchor.position);

            // Calculate the Z position of both left and right (via averaging). Then set the local position
            RDW.Instance.headPoseAnchor.localPosition = new Vector3(0f, 0f, (leftLocalPos.z + rightLocalPos.z)/2f);

            // Invoke any events
            onHeadPositionSet?.Invoke();
        }
    }
}