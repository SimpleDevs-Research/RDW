using System;
using System.Collections;
using UnityEngine;

namespace RDW {
    [System.Serializable]
    public class RecenterCalibration : CalibrationStep
    {
        [Header("=== References ===")]
        public OVRInput.Button calibrationFinishedInput;

        // Overriding the base `Calibrate` for our own head calibration.
        public override IEnumerator Calibrate() { 
            // Set our calibration status to `false`
            _calibrated = false;

            // While we're not calibrated, we will loop
            while(!_calibrated) {
                // Terminate if we're finished
                if (OVRInput.GetDown(calibrationFinishedInput)) _calibrated = true;
                // Make sure the update loop moves to the next frame
                yield return null;
            }
        }
    }
}