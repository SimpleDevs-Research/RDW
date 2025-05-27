using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW
{
    public class ManualGain2 : GainComponent2
    {
        Quaternion last_rotation;

        public override float CalculateGain(float deltaTime) {
            if (redirector == null || !active) return 0f;
            // Calculate change in rotation
            Quaternion cur_rotation = redirector.head_ref.rotation;
            Quaternion delta_rotation = cur_rotation * Quaternion.Inverse(last_rotation);
            delta_rotation.ToAngleAxis(out float angle, out Vector3 axis);
            // Guarantee that the rotation is around Y
            float yaw_delta = Vector3.Dot(axis, Vector3.up) * angle;
            // Record the last rotation for the next frame update
            last_rotation = cur_rotation; 
            // return the yaw delta
            return yaw_delta;
        }

        public override void Toggle() {
            base.Toggle();
            if (this.active) last_rotation = redirector.head_ref.rotation;
        }
    }
}
