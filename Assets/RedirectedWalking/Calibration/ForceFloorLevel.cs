using UnityEngine;
using UnityEngine.XR;

namespace RDW {
    public class ForceFloorLevel : MonoBehaviour
    {
        void Start() {
            // Give the Meta Link runtime a frame to finish handshaking, then override
            Invoke(nameof(SetFloorOrigin), 0.2f);
        }

        private void SetFloorOrigin() {
#if UNITY_2019_3_OR_NEWER
            // Modern Unity XR Subsystems approach
            var inputSubsystems = new System.Collections.Generic.List<XRInputSubsystem>();
            SubsystemManager.GetInstances(inputSubsystems);
            
            foreach (var subsystem in inputSubsystems) {
                if (subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor)) {
                    Debug.Log("Successfully forced tracking origin to Floor Level.");
                }
            }
#else
            // Legacy XR system fallback
            if (XRDevice.SetTrackingSpaceType(TrackingSpaceType.RoomScale)) {
                Debug.Log("Successfully forced legacy tracking space to RoomScale (Floor Level).");
            }
#endif
        }
    }
}
