using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    [System.Serializable]
    public class ManualGain2 : GainComponent2
    {
        [Header("=== Manual Gain ===")]
        [TextArea(4, 1000)]
        public string description = 
            "Manual Gain refers to a type of rotation gain that forces the redirection "
            + "to rotate at the same rate as the user's head. Use this for instances "
            + "when the user needs to make a rapid rotation, such as next to the play "
            + "space border.";

        public bool automated = false;
        public OVRInput.Button toggleButton = OVRInput.Button.Two;

        private Quaternion last_rotation;
        private Vector3 locked_pivot;

        public override void Enable() {
            base.Enable();
            if (automated && Boundary.Instance != null) {
                Boundary.Instance.onWithin.AddListener(this.ToggleOff);
                Boundary.Instance.onEdge.AddListener(this.ToggleOn);
            }
        }
        public override void Disable() {
            base.Disable();
            if (automated && Boundary.Instance != null) {
                Boundary.Instance.onWithin.RemoveListener(this.ToggleOff);
                Boundary.Instance.onEdge.RemoveListener(this.ToggleOn);
            }
        }

        public override void ToggleOn() {
            last_rotation = RDW.Instance.headPoseAnchor.rotation;
            locked_pivot = RDW.Instance.headPoseAnchor.position.Flatten();
            base.ToggleOn();
        }
        public void ToggleOn(Boundary.BoundaryInfo _) {
            last_rotation = RDW.Instance.headPoseAnchor.rotation;
            locked_pivot = RDW.Instance.headPoseAnchor.position.Flatten();
            base.ToggleOn();
        }
        public void ToggleOff(Boundary.BoundaryInfo _) {
            base.ToggleOff();
        }

        /*
        private void DeterminePivot(Redirector2 redirector) {
            // Pivot is dependent on two factors:
            // 1. Are we stationary or moving? If stationary, then we use the user's head as the pivot. If not, then we use one of the hands.
            // 2. Which direciton are we moving? If left, use the left pivot. If right, use the right pivot.
            // One concern: how do we incorporate controller inputs? If anything, controller inputs should dominate. 

            if (redirector.playerState == Redirector2.PlayerState.Standing) {
                // not moving, we can default to using the head as the pivot. This is non-conditional.
                locked_pivot = RDW.Instance.headPoseAnchor.position;
            }   
        }
        */

        public override float CalculateGain(Redirector2 redirector, float deltaTime) {
            // Active state is dependent on if the component's `activeState` matches the current state
            if (automated) {
                if ((activeState & redirector.playerState) != 0) {
                    if (!active) ToggleOn();
                } 
                else if (active) ToggleOff();
            }
            // This handles manual, not automated, input.
            //bool leftButtonDown = OVRInput.GetDown(OVRInput.Button.Four);
            //bool rightDownDown = OVRInput.GetDown(OVRInput.Button.Two);
            if (OVRInput.GetDown(toggleButton)) ToggleOn();
            if (OVRInput.GetUp(toggleButton)) ToggleOff();
            if (!active) {
                _contribution = 0f;
                return _contribution;
            }
            // Calculate change in rotation
            Quaternion cur_rotation = RDW.Instance.headPoseAnchor.rotation;
            Quaternion delta_rotation = cur_rotation * Quaternion.Inverse(last_rotation);
            delta_rotation.ToAngleAxis(out float angle, out Vector3 axis);
            // Guarantee that the rotation is around Y
            _contribution = Vector3.Dot(axis, Vector3.up) * angle;
            // Record the last rotation for the next frame update
            last_rotation = cur_rotation; 
            // return the yaw delta
            return _contribution;
        }

        /*
        public Vector3 GetLockedPivot() {
            return locked_pivot;
        }
        */
    }
}
