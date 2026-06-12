using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class Environment : MonoBehaviour
    {
        public static Environment Current;

        public enum BoundaryAnchor
        {
            Center,
            North, South, East, West,
            NorthEast, NorthWest, SouthEast, SouthWest,
        }

        public enum BoundaryScale {
            Percentage,
            Meters
        }

        [System.Serializable]
        public class BoundaryPosition {
            public BoundaryAnchor anchor;
            [Tooltip("Offset in meters")]
            public Vector2 offset;
        }
        
        [Header("=== Environment ===")]
        public Transform envRoot;

        [Header("=== Start Point Logic ===")]
        public Transform startPointRef;
        public BoundaryAnchor startRelativeTo = BoundaryAnchor.Center;
        public BoundaryScale startRelativeUnits = BoundaryScale.Percentage;
        [SerializeField, Tooltip("This is used for planning where the start is, relative to the Boundary. This scales the gizmos representing the boundary in the inspector; in runtime, the start position is divided by this value.")]
        private float startScale = 10f;
        [SerializeField, Tooltip("Should we start this environment on enable? Otherwise, wait until toggled.")]
        public bool startOnLoad = true;

        [Header("=== Data Cache - READ-ONLY ===")]
        [SerializeField] private Vector2 localBoundaryAnchor;
        [SerializeField] private Vector3 localStartPosition;
        [SerializeField] private Vector3 worldOffset;
        [SerializeField] private Vector3 worldStartPosition;

        public Vector3 envPosition => envRoot.position;
        public Quaternion envRotation => envRoot.rotation;

        private void OnDrawGizmos() {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, new Vector3(1f,0f,1f) * startScale);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, Vector3.right*startScale);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, Vector3.forward*startScale);
        }

        // When the world starts, we determine the placement of this object in relation to the start point
        private void OnEnable() {
            // To make it easier, let's set THIS environment as the static environment
            Current = this;

            // We need to calcualte the actual start point. 
            // Because the boundary is dynamic, for now we place ourselves at world center
            transform.position = (RDW.Instance != null)
                ? RDW.Instance.worldCenter 
                : Vector3.zero;

            // We need to determine the start point. This is based on the Boundary defined by the user.
            // For now, we need two things:
            // 1. The local position of the anchor point based on BoundaryAnchor.
            // 2. The local position of the start position relative to this transform
            localBoundaryAnchor = GetLocalAnchorPosition(startRelativeTo);
            localStartPosition = transform.InverseTransformPoint(startPointRef.position) / startScale;

            // Right now, both are in a scale between (-1:1, -1:1) relative to the center. 
            // For example, (0.5, 0.5) is the northeast corner; (-0.5, -0.5) is the southwest corner.
            // We need to calculate the offset from the anchor point in local space
            // So for example, if we have an anchor of (0.5,0.5) (northeast) and we have a start of (0.2, -0.1),
            // then offset (relative to the anchor) is (0.2, -0.1) - (0.5, 0.5) = (-0.3, -0.6)
            Vector2 offset = new Vector2(localStartPosition.x, localStartPosition.z) - localBoundaryAnchor;

            // The world space offset is defined by whether it should be in meters or relative.
            worldOffset = (startRelativeUnits == BoundaryScale.Meters)
                ? new Vector3(offset.x, 0f, offset.y) 
                : Boundary.Instance.transform.TransformVector(new Vector3(offset.x, 0f, offset.y));
            
            // Now, the true start point can be converted to Boundary space
            worldStartPosition = 
                Boundary.Instance.transform.TransformPoint(new Vector3(localBoundaryAnchor.x, 0f, localBoundaryAnchor.y)) 
                + worldOffset;
            
            // With that defined, we actually need to move `envRoot` such that 
            // the start ref is actually placed on top of `worldStartPosition`.
            // The fortunate thing is that since we know the world positions of both the start ref and world start position,
            // we can easily just translate `envRoot` to align
            envRoot.position += worldStartPosition - startPointRef.position;
            startPointRef.position = worldStartPosition;

            // At this point, we ask if we want to start the redirection and world upon completing the translation or not.
            if (startOnLoad) StartEnvironment();
            else envRoot.gameObject.SetActive(false);
        }

        private void OnDisable() {
            Current = null;
            if (Redirector2.Instance != null && Redirector2.Instance.environmentParent == this.transform) {
                Redirector2.Instance.environmentParent = null;
            }
        }

        public void StartEnvironment() {
            // Initialize the environment
            envRoot.gameObject.SetActive(true);
            if (Redirector2.Instance != null) {
                Redirector2.Instance.environmentParent = this.transform;
                // Initialize redirection
                Redirector2.Instance.Activate();
            }
        }

        // This is a special function event handler that can be called if the scene needs to be unloaded from within the scene.
        public void EnvironmentComplete() {
            if (RDW.Instance != null) RDW.Instance.UnloadEnvironment();
        }

        // Helper functions: Given a world position, direction, or rotation, return their local variants relative to `envRoot`.
        public Vector3 GetLocalPositionInEnv(Vector3 worldPosition) {
            return envRoot.InverseTransformPoint(worldPosition);
        }
        public Vector3 GetLocalDirectionInEnv(Vector3 worldDirection) {
            return envRoot.InverseTransformPoint(worldDirection);
        }
        public Quaternion GetLocalRotationInEnv(Quaternion worldRotation) {
            return Quaternion.Inverse(envRoot.rotation) * worldRotation;
        }

        // =====================================================================
        // Returns an anchor position relative to SW corner, from (-0.5,-0.5) to (0.5,0.5).
        // This is a local position, in other words.
        // =====================================================================
        public static Vector2 GetLocalAnchorPosition( BoundaryAnchor anchor ) {
            switch(anchor) {
                case BoundaryAnchor.NorthWest:  return new Vector2( -0.5f,  0.5f);
                case BoundaryAnchor.North:      return new Vector2(  0f,    0.5f);
                case BoundaryAnchor.NorthEast:  return new Vector2(  0.5f,  0.5f);

                case BoundaryAnchor.West:       return new Vector2( -0.5f,  0f);
                case BoundaryAnchor.Center:     return new Vector2(  0f,    0f);
                case BoundaryAnchor.East:       return new Vector2(  0.5f,  0f);

                case BoundaryAnchor.SouthWest:  return new Vector2( -0.5f, -0.5f);
                case BoundaryAnchor.South:      return new Vector2(  0f,   -0.5f);
                case BoundaryAnchor.SouthEast:  return new Vector2(  0.5f, -0.5f);
            }
            return Vector2.zero;  // center
        }

        // =====================================================================
        // Returns an anchor position that's not local. 
        // However, you must provide the center and bound size.
        // =====================================================================
        public static Vector2 GetAnchorPosition(
            BoundaryAnchor anchor,  // Enum value
            Vector2 center,         // (center.x, center.y)
            Vector2 size            // (width, height)
        ) {
            // Get the local anchor position
            Vector2 localPos = GetLocalAnchorPosition(anchor);

            // Now simply return the not-local anchor position
            return new Vector2(localPos.x * size.x, localPos.y * size.y) + center;
        }
    }
}
