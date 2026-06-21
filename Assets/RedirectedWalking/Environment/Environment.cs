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
        public EnvironmentRoot environmentRoot;

        [Header("=== Start Point Logic ===")]
        public Transform startPointRef;
        public BoundaryAnchor startRelativeTo = BoundaryAnchor.Center;
        public BoundaryScale startRelativeUnits = BoundaryScale.Percentage;
        [SerializeField, Tooltip("This is used for planning where the start is, relative to the Boundary. This scales the gizmos representing the boundary in the inspector; in runtime, the start position is divided by this value.")]
        private float startScale = 10f;
        [SerializeField, Tooltip("Should we start this environment on enable? Otherwise, wait until toggled.")]
        public bool startOnLoad = true;

        [Header("=== Debug ===")]
        public Transform boundaryDebugRef;

        [Header("=== Data Cache - READ-ONLY ===")]
        [SerializeField] private Vector2 localBoundaryAnchor;
        [SerializeField] private Vector3 localStartPosition;
        [SerializeField] private Vector3 worldOffset;
        [SerializeField] private Vector3 worldStartPosition;

        public Vector3 envPosition => environmentRoot.transform.position;
        public Quaternion envRotation => environmentRoot.transform.rotation;

        private void OnDrawGizmos() {
            // Draw scale cube
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, new Vector3(1f,0f,1f) * startScale);
            // Draw Scale Cube +X-Axis
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, Vector3.right*startScale);
            // Draw Scale Cube +Z-Axis
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, Vector3.forward*startScale);

            // Get what the local transform of `startPointRef ought to be
            Gizmos.color = Color.yellow;
            Vector3 localScaleStartPoint = startPointRef.position / startScale;
            Gizmos.DrawSphere(localScaleStartPoint, 0.25f);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(GetPointRelativeToBoundary(startPointRef.position, startRelativeTo, startRelativeUnits), 0.25f);
        }

        // When the world starts, we determine the placement of this object in relation to the start point
        private void OnEnable() {
            // To make it easier, let's set THIS environment as the static environment
            Current = this;

            // We need to calculate the actual start point. 
            // Because the boundary is dynamic, for now we place ourselves at world center
            transform.position = (RDW.Instance != null)
                ? RDW.Instance.worldCenter 
                : Vector3.zero;

            // We need to get the start point reference's position relative to the boundary.
            // Assuming that we are refering to the start point reference's world position, 
            // We can calculate that same world position in reference to our boundary
            Vector3 boundaryStartInWorld = GetPointRelativeToBoundary(
                startPointRef.position, 
                startRelativeTo, 
                startRelativeUnits
            );
            startPointRef.position = boundaryStartInWorld;

            // Now, let's assume our environment root (which should have an `EnvironmentRoot` component)
            // has its own starting point relative to itself. Our goal now is to make `EnvironmentRoot`
            // line up such that its starting point's orientation matches the orientation of `startPointRef`.

            // 1. Match Rotation
            environmentRoot.transform.rotation = startPointRef.rotation * Quaternion.Inverse(environmentRoot.startRef.localRotation);
            // 2. Match positions
            environmentRoot.transform.position = boundaryStartInWorld - environmentRoot.transform.rotation * environmentRoot.startRef.localPosition;

            if (Redirector2.Instance != null) {
                Redirector2.Instance.SetNonAgentParents(environmentRoot.transform);
            }
        }

        public void StartEnvironment() {
            // Initialize the environment
            environmentRoot.gameObject.SetActive(true);
            if (Redirector2.Instance != null) {
                // Initialize redirection
                Redirector2.Instance.SetEnvironmentParent(environmentRoot.transform);
                Redirector2.Instance.Activate();
            }
        }

        // This is a special function event handler that can be called if the scene needs to be unloaded from within the scene.
        public void EnvironmentComplete() {
            if (RDW.Instance != null) RDW.Instance.UnloadEnvironment();
        }

        private void OnDisable() {
            Current = null;
            if (Redirector2.Instance != null && Redirector2.Instance.environmentParent == environmentRoot.transform) {
                Redirector2.Instance.SetEnvironmentParent(null);
                Redirector2.Instance.SetNonAgentParents(null);
            }
        }

        
        // Helper functions: Given a world position, direction, or rotation, return their local variants relative to `environmentRoot`.
        public Vector3 GetLocalPositionInEnv(Vector3 worldPosition) {
            return environmentRoot.transform.InverseTransformPoint(worldPosition);
        }
        public Vector3 GetLocalDirectionInEnv(Vector3 worldDirection) {
            return environmentRoot.transform.InverseTransformPoint(worldDirection);
        }
        public Quaternion GetLocalRotationInEnv(Quaternion worldRotation) {
            return Quaternion.Inverse(environmentRoot.transform.rotation) * worldRotation;
        }

        private Vector3 GetPointRelativeToBoundary(Vector3 worldPosition, BoundaryAnchor anchor, BoundaryScale relative) {
            // Determine the transformation referencing the boundary. Fallback in case something happens
            Transform boundaryRef = (Boundary.Instance != null) 
                ? Boundary.Instance.transform
                : (boundaryDebugRef != null)
                    ? boundaryDebugRef
                    : this.transform;
            // Get the "local" position, as our start anchor was probably situated in world space
            Vector3 localPosition = worldPosition / startScale;
            // We also need to calculte our anchor's local position too
            Vector2 localAnchor = GetLocalAnchorPosition(anchor);
            Vector3 localAnchor3D = new Vector3(localAnchor.x, 0f, localAnchor.y);
            // We must calculate the offset between the anchor and local position. 
            Vector3 localPositionToAnchor = localPosition - localAnchor3D;
            // The moment of truth: what's the new start position relative to the boundary itself?
            // The calculation will differ based on if we want our relativity to be in meters or percentge
            //  - if in meters, then we only transform the local anchor to be relative to the boundary space scale. The offset is re-multiplied with the `startScale` and added.
            //  - if in percentag,e then it's really just the same as if we were to do `boundary.TrnsformPoint(localPosition)
            return (relative == BoundaryScale.Meters)
                ? boundaryRef.TransformPoint(localAnchor3D) + localPositionToAnchor * startScale
                : boundaryRef.TransformPoint(localAnchor3D + localPositionToAnchor);
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

        // ======================================================================
        // If your scene adds a query 
    }
}
