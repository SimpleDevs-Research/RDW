using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class Minimap : MonoBehaviour
    {

        public enum MovementStatus { Stationary, Moving }
        public enum RotationStatus { None, Left, Right }

        public Transform player;
        public Transform headPoseAnchor;
        
        public RectTransform headPoseAnchorSprite;
        public RectTransform pivotSprite;
        public RectTransform smoothPivotSprite;
        
        [SerializeField, Tooltip("{mapsScale} UI unit = 1 world meter")]
        private float mapScale = 20f;

        [SerializeField, Tooltip("At what translational speed to we consider the player moving or stationary?")]
        private float movingThreshold = 0.1f;
        [SerializeField, Tooltip("At what ")]
        private float rotationThreshold = 15f;
        [SerializeField, Tooltip("Smooth damp rate for pivot positioning")]
        private float pivotSmoothing = 1f;

        private Vector3 prevPosition;
        private Vector3 prevForward;
        [SerializeField] private MovementStatus movementStatus;
        [SerializeField] private RotationStatus rotationStatus;
        [SerializeField] private float rotationSpeed;
        private Vector2 pivotVelocity;

        private void Start() {
            CachePrevious();
        }

        private void CachePrevious() {
            prevPosition = headPoseAnchor.position;
            prevForward = headPoseAnchor.forward;
        }

        private void Update() {
            // Update Minimap
            Vector3 offset = headPoseAnchor.localPosition;
            Vector2 mapPosition = new Vector2(offset.x, offset.z) * mapScale;
            headPoseAnchorSprite.anchoredPosition = mapPosition;

            // Given the previous position and current position, determine the following:
            // 1. Movement Status: Am I stationary or moving?
            float deltaTime = Time.deltaTime;
            Vector3 displacement = headPoseAnchor.position - prevPosition;
            Vector3 horDisplacement = new Vector3(displacement.x, 0f, displacement.z);

            float speed = horDisplacement.magnitude/deltaTime;
            movementStatus = (speed < movingThreshold) ? MovementStatus.Stationary : MovementStatus.Moving;
            
            // 2. Rotation Status: Depending on movement status, are we moving left, right, or none?
            Vector3 radiusDir = Vector3.Cross(Vector3.up, horDisplacement).normalized;
            float signedAngle = Vector3.SignedAngle(
                Vector3.ProjectOnPlane(prevForward, Vector3.up),
                Vector3.ProjectOnPlane(headPoseAnchor.forward, Vector3.up),
                Vector3.up
            );
            rotationSpeed = signedAngle / deltaTime;
            float angleRad = Mathf.Abs(signedAngle) * Mathf.Deg2Rad;
            float radius = horDisplacement.magnitude / angleRad;

            // If `rotationSpeed` is negative here, then we're moving to the left. Otherwise, right.
            rotationStatus = (movementStatus == MovementStatus.Stationary)
                ? RotationStatus.None
                : (rotationSpeed < -rotationThreshold)
                    ? RotationStatus.Left 
                    : (rotationSpeed > rotationThreshold) 
                        ? RotationStatus.Right 
                        : RotationStatus.None;

            // Initialize Pivot based on `rotationSpeed`
            Vector3 pivot;
            switch(rotationStatus) {
                case RotationStatus.Left:
                    pivot = headPoseAnchor.position - Vector3.Cross(Vector3.up, horDisplacement.normalized).normalized * radius;
                    break;
                case RotationStatus.Right:
                    pivot = headPoseAnchor.position + Vector3.Cross(Vector3.up, horDisplacement.normalized).normalized * radius;
                    break;
                default:
                    pivot = headPoseAnchor.position;
                    break;
            }

            // Update pivot
            Vector3 localPivot = player.InverseTransformPoint(pivot);
            Vector2 targetPosition = new Vector2(localPivot.x, headPoseAnchor.localPosition.z) * mapScale;
            pivotSprite.anchoredPosition = targetPosition;
            smoothPivotSprite.anchoredPosition = Vector2.SmoothDamp(
                smoothPivotSprite.anchoredPosition,
                targetPosition,
                ref pivotVelocity,
                pivotSmoothing * deltaTime
            );
            // At the end, cache
            CachePrevious();
        }


    }
}
