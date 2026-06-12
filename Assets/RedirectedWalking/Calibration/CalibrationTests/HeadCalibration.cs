using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace RDW {
    [System.Serializable]
    public class HeadCalibration : CalibrationStep
    {   

        public class CenterRadiusSample {
            public Vector3 point;
            public Vector3 direction;
            public CenterRadiusSample(Vector3 p, Vector3 d) {
                point = p;
                direction = d;
            }
        }

        [Header("=== References ===")]
        public TextMeshProUGUI offsetTextboxRef = null;
        public OVRInput.Button calibrateHeadInput;
        public OVRInput.Button calibrationFinishedInput;

        [Header("=== Step-Specific Event Handling ===")]
        public UnityEvent onSamplingStart;
        public UnityEvent onSamplingEnd;

        [Header("=== On Head Center Sampling ===")]
        public float samplingDuration = 5f;
        public bool sampling = false;
        private List<CenterRadiusSample> samples;
        public float estimatedHeadPoseDisplacement = 0f;
        
        public override IEnumerator Initialize() {
            estimatedHeadPoseDisplacement = Player.Instance.headPoseOffset;
            // From the original code
            OnCalibrationStart?.Invoke();
            yield return StartCoroutine(Calibrate());
            OnCalibrationEnd?.Invoke();
        }

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
                // Only invoke the calibration step if the user clicked the `calibrateHeadInput` button and we aren't sampling
                if (OVRInput.GetDown(calibrateHeadInput) && !sampling) yield return StartCoroutine(SampleCoroutine());
                // Terminate if we're finished and we're not sampling
                if (OVRInput.GetDown(calibrationFinishedInput) && !sampling) _calibrated = true;
                // Fill the offset textbox if set
                if (offsetTextboxRef != null) offsetTextboxRef.text = estimatedHeadPoseDisplacement.ToString();
                // Make sure the update loop moves to the next frame
                yield return null;
            }

            // Update the head pose anchor
            Player.Instance.UpdateHeadPoseOffset(estimatedHeadPoseDisplacement);
        }

        // Overriding the base `SetCalibrated` to account for sampling. We don't want to cancel out if we're sampling
        public override void SetCalibrated(bool setTo) { if (!sampling) base.SetCalibrated(setTo); }

        public void StartSampling() {
            // Initialize sampling coroutine if not sampling
            if (!sampling) StartCoroutine(SampleCoroutine());
        }

        private IEnumerator SampleCoroutine() {
            // Set state of sampling
            sampling = true;

            // Initialize samples list and head pose displacement estimate
            samples = new List<CenterRadiusSample>();
            estimatedHeadPoseDisplacement = 0f;

            // Initialize delay
            WaitForSeconds sampleDelay  = new WaitForSeconds(0.1f);
            float startTime = Time.time;

            // Invoke any events on start
            onSamplingStart?.Invoke();
            
            // Initialize while loop
            while (Time.time - startTime < samplingDuration) {
                // Add new sample
                samples.Add(new CenterRadiusSample(RDW.Instance.centerEyeCamera.position, -RDW.Instance.centerEyeCamera.forward));

                // Estimations
                estimatedHeadPoseDisplacement = -EstimateRadius(samples, EstimateCenter(samples));

                // Report
                if (offsetTextboxRef != null) offsetTextboxRef.text = estimatedHeadPoseDisplacement.ToString();

                // Delay
                yield return sampleDelay;
            }

            // At the end of sampling, we estimate one more time for good measure
            estimatedHeadPoseDisplacement = -EstimateRadius(samples, EstimateCenter(samples));

            // Call any events at the end
            onSamplingEnd?.Invoke();

            // Now disable the sampling flag
            sampling = false;
        }

        public static Vector3 EstimateCenter(List<CenterRadiusSample> points) {
            Vector3 centerSum = Vector3.zero;
            int count = 0;

            for (int i = 0; i < points.Count; i++) {
                for (int j = i + 1; j < points.Count; j++) {
                    if (ClosestPointsOnLines(
                        points[i], 
                        points[j],
                        out Vector3 c1,
                        out Vector3 c2)
                    ) {
                        centerSum += (c1 + c2) * 0.5f;
                        count++;
                    }
                }
            }
        
            return centerSum / count;
        }

        public static bool ClosestPointsOnLines(
            CenterRadiusSample s1, 
            CenterRadiusSample s2, 
            out Vector3 c1,
            out Vector3 c2
        ) {
            c1 = Vector3.zero;
            c2 = Vector3.zero;

            float a = Vector3.Dot(s1.direction, s1.direction);
            float b = Vector3.Dot(s1.direction, s2.direction);
            float e = Vector3.Dot(s2.direction, s2.direction);

            float denom = a * e - b * b;
            if (Mathf.Abs(denom) < 1e-6f)
                return false;

            Vector3 r = s1.point - s2.point;

            float c = Vector3.Dot(s1.direction, r);
            float f = Vector3.Dot(s2.direction, r);

            float s = (b * f - c * e) / denom;
            float t = (a * f - b * c) / denom;

            c1 = s1.point + s1.direction * s;
            c2 = s2.point + s2.direction * t;

            return true;
        }

        public static float EstimateRadius(List<CenterRadiusSample> points, Vector3 center) {
            float radius = 0f;
            foreach (var p in points) {
                radius += Vector3.Distance(center, p.point);
            }
            return radius / points.Count;
        }
    }
}