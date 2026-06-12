using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace RDW {
    public class Player : MonoBehaviour
    {
        public static Player Instance;

        public enum TranslationStatus   { Stationary, Moving }
        public enum TurnStatus          { None, Left, Right }


        [System.Serializable]
        public class Status {
            public Vector3 Position;
            public Vector3 Forward;
            public Vector3 Pivot;
            public TranslationStatus Translating = TranslationStatus.Stationary;
            public TurnStatus Turning = TurnStatus.None;
        }

        [Header("=== World Space References ===")]
        public Transform centerEyeCamera;
        public Transform headPoseAnchor;
        public Transform pivotRef;
        [Space]
        public Transform leftHandAnchor;
        public Transform rightHandAnchor;

        [Header("=== Settings ===")]
        [SerializeField, Tooltip("At what translational speed to we consider the player moving or stationary?")]
        private float movingThreshold = 0.1f;
        [SerializeField, Tooltip("At what rotational speed (degrees) do we consider the player rotating or not?")]
        private float turningThreshold = 15f;
        [SerializeField, Tooltip("Smooth damp rate for pivot positioning")]
        private float pivotSmoothing = 8f;
        [SerializeField, Tooltip("What's the offset between the eye cam and head pose anchor, in local space?")]
        private float _headPoseOffset = -0.1f;
        public float headPoseOffset => _headPoseOffset;

        [Header("=== UI Minimap (Optional) ===")]
        [SerializeField]
        private RectTransform headPoseAnchorSprite;
        [SerializeField]
        private RectTransform pivotSprite;
        [SerializeField]
        private RectTransform smoothPivotSprite;
        [SerializeField]
        private RectTransform leftHandSprite;
        [SerializeField]
        private RectTransform rightHandSprite;
        [SerializeField, Tooltip("{mapsScale} UI unit = 1 world meter")]
        private float mapScale = 20f;
        [SerializeField]
        private TextMeshProUGUI translationTextbox;
        [SerializeField]
        private TextMeshProUGUI turnTextbox;

        private Status _currentState;
        public Status CurrentState => _currentState;
        private Status _previousState;
        public Status PreviousState => _previousState;
        private Vector3 pivotVelocity;

        private void Awake() {
            Instance = this;
        }

        private void Start() {
            _currentState = new Status {
                Position = headPoseAnchor.position,
                Forward = headPoseAnchor.forward
            };
            _previousState = new Status {
                Position = headPoseAnchor.position,
                Forward = headPoseAnchor.forward
            };
            headPoseAnchor.localPosition = new Vector3(0f, 0f, _headPoseOffset);
        }

        private void OnEnable() {
            // Current state gets updated anyways in update loop
            CachePreviousState();
        }

        private void LateUpdate() {
            // ===========================
            // CURRENT CACHE THIS FRAME
            // ===========================
            _currentState.Position = headPoseAnchor.position;
            _currentState.Forward = headPoseAnchor.forward;

            // ===========================
            // DELTA TIME AND DISPLACEMENT
            // ===========================
            float deltaTime = Time.deltaTime;
            Vector3 displacement = _currentState.Position - _previousState.Position;
            Vector3 horizontalDisplacement = new Vector3(displacement.x, 0f, displacement.z);

            // ============================
            // SPEED & TRANSLATING UPDATE 
            // ============================
            float speed = horizontalDisplacement.magnitude / deltaTime;
            _currentState.Translating = (speed < movingThreshold) 
                ? TranslationStatus.Stationary 
                : TranslationStatus.Moving;

            // =============================
            // TURN ANGLE AND TURNING UPDATE
            // 1. Calculate the ray perpendicular to the horizontal forward movement
            // 2. The angle or turning is defined as a signed angle (<0 = left, >0 = right) 
            // =============================
            Vector3 radiusDirection = Vector3.Cross(Vector3.up, horizontalDisplacement).normalized;
            float signedAngle = Vector3.SignedAngle(
                Vector3.ProjectOnPlane(_previousState.Forward, Vector3.up),
                Vector3.ProjectOnPlane(_currentState.Forward, Vector3.up),
                Vector3.up
            );
            float rotationSpeed = signedAngle / deltaTime;
            _currentState.Turning = (_currentState.Translating == TranslationStatus.Stationary)
                ? TurnStatus.None
                : (rotationSpeed < -turningThreshold)
                    ? TurnStatus.Left 
                    : (rotationSpeed > turningThreshold) 
                        ? TurnStatus.Right 
                        : TurnStatus.None;

            // ==============================
            // RADIUS & RAW PIVOT UPDATE
            // ============================== 
            float radius = horizontalDisplacement.magnitude / (Mathf.Abs(signedAngle) * Mathf.Deg2Rad);
            Vector3 rawPivotPoint = _currentState.Position;
            switch(_currentState.Turning) {
                case TurnStatus.Left:
                    rawPivotPoint -= Vector3.Cross(Vector3.up, horizontalDisplacement.normalized).normalized * radius;
                    break;
                case TurnStatus.Right:
                    rawPivotPoint += Vector3.Cross(Vector3.up, horizontalDisplacement.normalized).normalized * radius;
                    break;
            }

            // ==============================
            // RAW PIVOT LOCAL POSITION CORRECTION
            // ==============================
            Vector3 localRawPivot = centerEyeCamera.InverseTransformPoint(rawPivotPoint);
            localRawPivot = new Vector3(localRawPivot.x, 0f, headPoseAnchor.localPosition.z);

            // ==============================
            // SMOOTH PIVOT UPDATE
            // ==============================
            Vector3 localSmoothPivot = Vector3.SmoothDamp(
                pivotRef.localPosition,
                localRawPivot,
                ref pivotVelocity,
                pivotSmoothing * deltaTime
            );
            _currentState.Pivot = centerEyeCamera.TransformPoint(localSmoothPivot);

            // ==============================
            // WORLD SPACE UPDATES
            // ==============================
            pivotRef.localPosition = localSmoothPivot;
            if (headPoseAnchorSprite != null) 
                headPoseAnchorSprite.anchoredPosition = new Vector2(
                    headPoseAnchor.localPosition.x, 
                    headPoseAnchor.localPosition.z
                ) * mapScale;
            if (pivotSprite != null) {
                pivotSprite.anchoredPosition = new Vector2(
                    localRawPivot.x,
                    localRawPivot.z
                ) * mapScale;
            }
            if (smoothPivotSprite != null) {
                smoothPivotSprite.anchoredPosition = new Vector2(
                    localSmoothPivot.x,
                    localSmoothPivot.z
                ) * mapScale;
            }
            if (leftHandSprite != null) {
                Vector3 leftHandLocal = centerEyeCamera.InverseTransformPoint(leftHandAnchor.position);
                leftHandSprite.anchoredPosition = new Vector2(leftHandLocal.x, leftHandLocal.z) * mapScale;
            }
            if (rightHandSprite != null) {
                Vector3 rightHandLocal = centerEyeCamera.InverseTransformPoint(rightHandAnchor.position);
                rightHandSprite.anchoredPosition = new Vector2(rightHandLocal.x, rightHandLocal.z) * mapScale;
            }
            if (translationTextbox != null) translationTextbox.text = _currentState.Translating.ToString();
            if (turnTextbox != null) turnTextbox.text = _currentState.Turning.ToString();
            
            // ===============================
            // CACHING PREVIOUS FOR NEXT FRAME
            // ===============================
            CachePreviousState();
        }

        private void CachePreviousState() {
            _previousState.Position = _currentState.Position;
            _previousState.Forward = _currentState.Forward;
            _previousState.Pivot = _currentState.Pivot;
            _previousState.Translating = _currentState.Translating;
            _previousState.Turning = _currentState.Turning;
        }

        public void UpdateHeadPoseOffset(float o) {
            _headPoseOffset = o;
            headPoseAnchor.localPosition = new Vector3(0f, 0f, o);
        }
    }
}