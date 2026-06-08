using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace RDW {
    [System.Serializable]
    public class EdgeDistanceCalibration : CalibrationStep
    {
        [Header("=== References ===")]
        public TextMeshProUGUI distanceTextbox;
        public OVRInput.Button resetDistanceCalculationInput = OVRInput.Button.Two;
        public OVRInput.Button calibrationFinishedInput = OVRInput.Button.One;

        [Header("=== Data Cache ===")]
        public float boundaryDistance = 0f;

        // The calibration operation and update loop. 
        // Is a coroutine, so must be instantiated via `StartCoroutine()`.
        public override IEnumerator Calibrate() { 
            // Ensure that calibration is reset
            _calibrated = false;
            ResetBoundaryDistance();

            // While loop to simulate Update
            while(!_calibrated) {
                // Get the minimum between the current boundary distance and the calculated one
                boundaryDistance = Mathf.Min(boundaryDistance, RDW.Instance.GetMinDistanceToRectangleEdge());
                // Update the textbox if set
                if (distanceTextbox != null) distanceTextbox.text = boundaryDistance.ToString();
                // Reset distance if toggled
                if (OVRInput.GetDown(resetDistanceCalculationInput)) ResetBoundaryDistance();
                // Detect end via button click
                if (OVRInput.GetDown(calibrationFinishedInput)) _calibrated = true;
                // next frame
                yield return null;
            }

            // Update RDW with this info
            RDW.Instance.minEdgeDistance = boundaryDistance;

            // Yield return null for good measure
            yield return null; 
        }

        public void ResetBoundaryDistance() {
            // The biggest possible distance is the boundary distance at world center.
            boundaryDistance = RDW.Instance.GetMinDistanceToRectangleEdge(RDW.Instance.worldCenter);
        }

    }
}