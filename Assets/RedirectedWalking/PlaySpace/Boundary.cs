using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    [RequireComponent(typeof(BoxCollider))]
    public class Boundary : MonoBehaviour
    {
        public static Boundary Instance;
        public enum BoundaryStatus { Off, Within, Approaching, Edge, Outside }

        [System.Serializable]
        public class BoundaryInfo {
            public Vector3 Point;
            public float Distance;
            public BoundaryStatus Status;
        }

        private static readonly int PlayerPositionID =
            Shader.PropertyToID("_PlayerPosition");
        private static readonly int WarningDistanceID = 
            Shader.PropertyToID("_WarningDistance");

        [Header("=== References ===")]
        [SerializeField, Tooltip("Reference the user's camera or head pose anchor")] 
        private Transform player;
        [SerializeField, Tooltip("The material for the boundary")] 
        private Material boundaryMaterial;
        // The bounding box collider that defines the edges of the space
        private BoxCollider collider;

        [SerializeField, Tooltip("The distance (in world space) where the player is considered approaching the boundary edge")]
        private float _approachingDistance;
        public float approachingDistance => _approachingDistance;
        [SerializeField, Tooltip("The distance (in world space) where any warnings should be invoked")]
        private float _warningDistance;
        public float warningDistance => _warningDistance;

        [Tooltip("The current status of the player with respect to the boundary")]
        public BoundaryStatus status = BoundaryStatus.Within;
        public BoundaryStatus prevStatus;

        [Tooltip("Any events we should invoke upon the change in state")]
        public UnityEvent<BoundaryInfo> onWithin, onApproaching, onEdge, onOutside;
        public UnityEvent onOff;

        [SerializeField, Tooltip("Cache: The closest point to the boundary from the player's position")] 
        private BoundaryInfo _playerInfo;
        public BoundaryInfo playerInfo => _playerInfo;

        // Getters
        public Vector2 size => new Vector2(transform.localScale.x, transform.localScale.z);
        public float distance => _playerInfo.Distance;
        public string boundaryStatusStr => status.ToString();

        private void Awake() {
            Instance = this;

            // Additional things to do to our collider to ensure no physics interactions and make sure it matches our parent transform's position
            collider = GetComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = Vector3.one;
            collider.center = new Vector3(0f, 0.5f, 0f);
        }

        private void OnEnable() {
            // Make sure we cache some previous status
            UpdateStatus();
            prevStatus = _playerInfo.Status;
        }

        private void Update() {
            
            // Can't do anything if there's no player reference
            if (player == null) {
                _playerInfo.Status = BoundaryStatus.Off;
                return;
            }
            
            // Update the player's position in the boundary shader
            boundaryMaterial.SetVector( PlayerPositionID, player.position);

            // Update our status, invoke any events if needed
            UpdateStatus();
            if (prevStatus != _playerInfo.Status) {
                switch(_playerInfo.Status) {
                    // `Approaching` and `Edge` are within-relative. They won't be called if you're moving from outside-inward
                    case BoundaryStatus.Approaching:
                        if (prevStatus == BoundaryStatus.Within) 
                            onApproaching?.Invoke(_playerInfo);
                        break;
                    case BoundaryStatus.Edge:
                        if (prevStatus == BoundaryStatus.Within || prevStatus == BoundaryStatus.Approaching)
                            onEdge?.Invoke(_playerInfo);
                        break;
                    // After this, we just treat within and outside as-is
                    case BoundaryStatus.Within:
                        onWithin?.Invoke(_playerInfo);
                        break;
                    case BoundaryStatus.Outside:
                        onOutside?.Invoke(_playerInfo);
                        break;
                    default:    // Off
                        onOff?.Invoke();
                        break;
                }
                prevStatus = _playerInfo.Status;
            }
        }

        public void SetPlayer(Transform t) {
            player = t;
        }
        public void SetApproachDistance(float d) {
            _approachingDistance = d;
            boundaryMaterial.SetFloat(WarningDistanceID, d*2f);
        }
        public void SetWarningDistance(float d) {
            _warningDistance = d;
        }

        // This is a redundant function if the collider's bounds match exactly the scale of the parent transform.
        // However, if the collider is offset, resized, or repositioned in any way, then we need to account for that.
        // However, it should be emphasized that the bounding box should be automatically defaulted to match
        // the local scale of the parent transform. This is still here as a reminder.
        public Vector3 GetLocalPosition(Vector3 worldPosition) {
            Vector3 localPos = collider.transform.InverseTransformPoint(worldPosition);
            return localPos - collider.center;
        }
        public Vector3 GetWorldPosition(Vector3 localPosition) {
            Vector3 localPos = localPosition + collider.center;
            return collider.transform.TransformPoint(localPos);
        }

        // Other helpful public getters
        public Vector3 GetLocalDirection(Vector3 worldDirection) {
            // This returns a normalized direction. Doesn't return actual lengthed vectors
            return collider.transform.InverseTransformDirection(worldDirection);
        }
        public Quaternion GetLocalRotation(Quaternion worldRotation) {
            return Quaternion.Inverse(collider.transform.rotation) * worldRotation;
        }

        // This is a function to get the closest edge point along the collider.
        // We want to ignore the y-axis, so we only look at the x and z position
        public Vector3 GetClosestBoundaryPoint(Vector3 worldPosition, out Vector3 localPos) {
            localPos = GetLocalPosition(worldPosition);
            Vector3 halfSize = collider.size * 0.5f;

            float distToPosX = halfSize.x - localPos.x;
            float distToNegX = localPos.x + halfSize.x;
            float distToPosZ = halfSize.z - localPos.z;
            float distToNegZ = localPos.z + halfSize.z;

            float minDist = Mathf.Min(
                distToPosX,
                distToNegX,
                distToPosZ,
                distToNegZ
            );

            Vector3 closestLocal = localPos;
            if (minDist == distToPosX)
                closestLocal.x = halfSize.x;
            else if (minDist == distToNegX)
                closestLocal.x = -halfSize.x;
            else if (minDist == distToPosZ)
                closestLocal.z = halfSize.z;
            else
                closestLocal.z = -halfSize.z;

            // Return in world space
            return GetWorldPosition(closestLocal);
        }
        public Vector3 GetClosestBoundaryPoint(Vector3 worldPosition) {
            return GetClosestBoundaryPoint(worldPosition, out Vector3 _);
        }

        // This is a function to get the closest distance to the edge. To do this, we need 
        // to get the closest edge point (which must be in world space) and calculate the distance. 
        // So inevitably, this is doing the same thing as `GetClosestBoundaryPoint` with an additional 
        // operation on top of it. 
        // Note that we also want to preserve sign to indicate outside or inside.
        public float GetDistanceToBoundary(Vector3 worldPosition, out Vector3 closestPoint) {
            // Calculate the closest edge point and distance
            closestPoint = GetClosestBoundaryPoint(worldPosition, out Vector3 localPosition);
            float d = Vector3.Distance(worldPosition, closestPoint);

            // Calculate the sign from local positioning
            Vector3 halfSize = collider.size * 0.5f;
            bool inside = 
                Mathf.Abs(localPosition.x) <= halfSize.x 
                && Mathf.Abs(localPosition.z) <= halfSize.z;

            // return the distance based on `inside`
            return inside ? d : -d;
        }
        public float GetDistanceToBoundary(Vector3 worldPosition) {
            return GetDistanceToBoundary(worldPosition, out Vector3 _);
        }
        public float GetDistanceToBoundary() {
            return GetDistanceToBoundary(player.position, out Vector3 _);
        }

        public void UpdateStatus() {
            // Return `off` if no player is referenced
            if (player == null) {
                _playerInfo.Status = BoundaryStatus.Off;
                return;
            }

            // Call `GetDistanceToBoundary` as it performs both operations
            _playerInfo.Distance = GetDistanceToBoundary(player.position, out _playerInfo.Point);

            // Set the appropriate status depending on the updated edge info
            if (_playerInfo.Distance < 0f) _playerInfo.Status = BoundaryStatus.Outside;
            else if (_playerInfo.Distance <= _warningDistance) _playerInfo.Status = BoundaryStatus.Edge;
            else if (_playerInfo.Distance <= _approachingDistance) _playerInfo.Status = BoundaryStatus.Approaching;
            else _playerInfo.Status = BoundaryStatus.Within;
        }

        // This is a helper function available to the public. 
        // It returns BoundaryInfo for any query world position
        public BoundaryInfo GetClosestBoundaryInfo(Vector3 worldPosition) {
            // Since `GetDistanceToBoundary` already performs the closest point calc,
            // We just use that conveniently.
            float closestDistance = GetDistanceToBoundary(worldPosition, out Vector3 closestPoint);
            return new BoundaryInfo {
                Point = closestPoint,
                Distance = closestDistance
            };
        }

        public void LogDebug(string m) { Debug.Log(m); }
    }
   
}