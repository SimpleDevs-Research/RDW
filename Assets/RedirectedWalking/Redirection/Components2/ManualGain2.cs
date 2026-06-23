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
        //public Vector3 pivot_offset = Vector3.zero;

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
            //pivot_offset = Vector3.zero;
            base.ToggleOn();
        }
        public void ToggleOn(Boundary.BoundaryInfo _) {
            last_rotation = RDW.Instance.headPoseAnchor.rotation;
            //pivot_offset = Vector3.zero;
            base.ToggleOn();
        }
        public void ToggleOff(Boundary.BoundaryInfo _) {
            //pivot_offset = Vector3.zero;
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
            
            // ===================
            // Gain Contribution Calculation
            // ===================
            // Calculate change in rotation
            Quaternion cur_rotation = RDW.Instance.headPoseAnchor.rotation;
            Quaternion delta_rotation = cur_rotation * Quaternion.Inverse(last_rotation);
            delta_rotation.ToAngleAxis(out float angle, out Vector3 axis);
            // Guarantee that the rotation is around Y
            _contribution = Vector3.Dot(axis, Vector3.up) * angle;
            // Record the last rotation for the next frame update
            last_rotation = cur_rotation;

            /*
            // ===================
            // Pivot offset calculation
            // ===================
            // Position displacement in world space
            Vector3 pivotToPlayer = Player.Instance.CurrentState.Position - Player.Instance.CurrentState.Pivot;
            pivotToPlayer.y = 0f;
            // Radial (toward/away from pivot) and Tangential (around the pivot) Directions
            Vector3 radialDir = pivotToPlayer.normalized;
            Vector3 tangentDir = Vector3.Cross(Vector3.up, radialDir);
            float radialDisplacement = Vector3.Dot(Player.Instance.CurrentState.HorizontalDisplacement, radialDir);
            float tangentialDisplacement = Vector3.Dot(Player.Instance.CurrentState.HorizontalDisplacement, tangentDir);
            // Meaning:
            // - radialDisplacement > 0       --> player moved away from pivot
            // - radialDisplacement < 0       --> player moved toward pivot
            // - tangentialDisplacement > 0   --> player moved clockwise around pivot (depending on basis orientation)
            // - tangentialDisplacement < 0   --> player moved counterclockwise
            // Radial and Tangential Displacement
            float sidewaysDisplacement = Vector3.Dot(Player.Instance.CurrentState.HorizontalDisplacement, radialDir);
            float forwardDisplacement = Vector3.Dot(Player.Instance.CurrentState.HorizontalDisplacement, tangentDir);
            // Offset setting
            pivot_offset = new Vector3(sidewaysDisplacement, 0f, forwardDisplacement);
            */

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
