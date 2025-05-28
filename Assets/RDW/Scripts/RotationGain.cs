using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    // Rotational Gain refers to angular gain achieved during the head's yaw rotation. 
    //      Rate diffs whether the direction of head rotation matches redirection rotation. 
    //      We can rotate via degrees/sec, or by ratio.
    public class RotationGain : GainComponent
    {
        [Header("Rotational Gain Options")]
        [Tooltip("The rotational gain for same-direction rotation. Steinicke et al. would suggest a rate of 0.49 for same-direction rotation.")]
        public float rotationGainSame = 0.49f;
        [Tooltip("The rotational gain for opposite-direction rotation. Steinicke et al. would suggest a rate of -0.2 for opposite-direction rotation.")]
        public float rotationGainOpposite = -0.2f;

        [Header("Rotation Threshold.")]
        [HelpAttribute("These settings and controls are either rare or completely unique to this implementation.")]
        [Tooltip("Minimum velocity threshold (degrees/sec) of the head for rotational gain to be applied. Suggested by Hodgson and Bachmann.")]
        public float rotationMinThreshold = 1.5f;

        public override float CalculateGain(float deltaTime) {
             // What's the difference in angle between the previous head orientation and the current head orientation, relative to the horizon?
            // Note: - = leftward, + = rightward
            float angleRotation = redirector.current_head_rotation;
            float angleRotSign = Mathf.Sign(angleRotation);
            float angleMagnitude = Mathf.Abs(angleRotation);
        
            // What's the velocity of the head rotation?
            // We only contribute if the velocity matches a threshold
            float angleVel = angleMagnitude/deltaTime;
            if (angleVel < rotationMinThreshold) return 0f;

            // Based on `angleRotation` (+ = rightward, - = leftward), calculate the rotational gain
            float redirectionSign = Mathf.Sign(redirector.direction_factor);
            // In the case of the direction of the redirection being the same as the head rotation, then the angle influence is RotationGainSame.
            // However, in the case where the direction of redirection is NOT the same as the head rotation, then the angle influence is RotationGainOpposite
            return (redirectionSign == angleRotSign) 
                ? angleRotation * rotationGainSame * redirector.speed_factor
                : angleRotation * rotationGainOpposite * redirector.speed_factor;
        }
    }
}
