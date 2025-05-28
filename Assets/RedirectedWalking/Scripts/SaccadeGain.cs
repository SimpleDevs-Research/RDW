using System;
using UnityEngine;
using Meta.XR.Util;

namespace RDW {
    public class SaccadeGain : GainComponent
    {
        [SerializeField, Tooltip("What angular velocity threshold should be used to discretize if it's a saccade or not?")]
        public float saccadeAngleThreshold = 180f;
        [Tooltip("The redireciton gain from saccade.")]
        public float saccadeGain = 12.6f;

        public override float CalculateGain(float deltaTime) {
            // Measure the 
            if (redirector.current_eye_rotation <= saccadeAngleThreshold * Time.deltaTime) return 0f;
            return saccadeGain * deltaTime * redirector.direction_factor * redirector.speed_factor;
        }
    }
}