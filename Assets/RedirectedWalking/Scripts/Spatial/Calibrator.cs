using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using TMPro;

namespace RDW {
    public class Calibrator : MonoBehaviour
    {
        public static Calibrator Instance;
        public enum CalibrationHand { Left, Right }

        [Header("=== References ===")]
        public Transform floor;
        public LineRenderer spaceLineRenderer;
        public InstructionsCanvas instructionsCanvas;
        public Transform minSpaceAnchor, maxSpaceAnchor;

        [Header("=== Calibration Setup ===")]
        [Tooltip("Prior to calibration, if there's any events you want to invoke, do so here")]
        public UnityEvent onPlaySpaceCalibrationStart;
        [Tooltip("All calibration steps to be run in order.")]
        public List<CalibrationStep> calibrationSteps;
        [Tooltip("Upon completing calibration, if there's any events you want to invoke, do so here")]
        public UnityEvent onPlaySpaceCalibrationEnd;

        [Header("=== Debugging ===")]
        public Transform debugRayIntersectionRef;
        public TextMeshPro debugTextbox;

        private void Awake() {
            // create a new persistent instance
            Instance = this;
        }

        private void OnEnable() {
            // Modify RDW to prevent interference
            RDW.Instance.ResetSpace();
            floor.localScale = new Vector3(10f, 10f, 1f);

            // Have the floor and min/max space anchors match the world positions of the play space, at least initially
            AlignWithPlaySpace();

            // Initialize the line renderer
            if (spaceLineRenderer != null) spaceLineRenderer.positionCount = 4;

            // Initialize by calling the calibration
            StartCoroutine(Calibrate());
        }

        private IEnumerator Calibrate() {

            // If any events need to be called, do so here.
            onPlaySpaceCalibrationStart?.Invoke();

            // Run each calibration step in order
            if (calibrationSteps.Count > 0) foreach(CalibrationStep step in calibrationSteps) {
                yield return step.Initialize();
            }

            // Align our floor and min-max to be aligned with the bounds again
            AlignWithPlaySpace();

            // If any events need to be called, do them here.
            onPlaySpaceCalibrationEnd?.Invoke();
        }

        // =======================
        // === DEBUG PURPOSES ===
        // =======================
        private void Update() {
            // If a line renderer is provided, make sure it is aligned to the anchor spaces
            if (spaceLineRenderer != null) {
                // Set each position
                spaceLineRenderer.SetPosition(0, maxSpaceAnchor.position);
                spaceLineRenderer.SetPosition(1, new Vector3(
                    maxSpaceAnchor.position.x, 
                    (maxSpaceAnchor.position.y + minSpaceAnchor.position.y)/2f,
                    minSpaceAnchor.position.z
                ));
                spaceLineRenderer.SetPosition(2, minSpaceAnchor.position);
                spaceLineRenderer.SetPosition(3, new Vector3(
                    minSpaceAnchor.position.x,
                    (maxSpaceAnchor.position.y + minSpaceAnchor.position.y)/2f,
                    maxSpaceAnchor.position.z
                ));
            }

            //if (debugTextbox != null) debugTextbox.text = $"{minSpaceBound}\n{maxSpaceBound}\n{calibrated}";
            if (debugRayIntersectionRef != null) {
                debugRayIntersectionRef.position = RDW.Instance.GetEdgePointFromRay();
            }
        }

        public void AlignWithPlaySpace() {
            floor.position = RDW.Instance.playSpace.TransformPoint(RDW.Instance.worldCenter);
            minSpaceAnchor.position = RDW.Instance.playSpace.TransformPoint(RDW.Instance.minSpaceBound);
            maxSpaceAnchor.position = RDW.Instance.playSpace.TransformPoint(RDW.Instance.maxSpaceBound);
        }
    }

}