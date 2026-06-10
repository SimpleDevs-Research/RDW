using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    [System.Serializable]
    public class SaccadeGain2 : GainComponent2
    {
        [Header("=== Saccade Gain ===")]
        [TextArea(4,1000)]
        public string description = "Saccade Gain refers to the gain obtained while the user's gaze is "
                                    + "performing a saccade maneuver. Saccades are ballistic in nature and "
                                    + "behave in a way that lets us take advantage of the \"saccadic suppression\" " 
                                    + "phenomenon experienced during them. According to Sun et al., the recommended "
                                    + "saccade gain allowed is approximately 12.6 degrees/sec if the saccade detection "
                                    + "threshold (the speed the eye rotates) is 180 degrees/sec";
        [Space]
        [SerializeField, Tooltip("The angular velocity threshold for detecting if an eye rotation is a saccade (degrees/sec)")]
        public float saccadeThreshold = 180f;
        [Tooltip("The redireciton gain from saccade (degrees/sec).")]
        public float saccadeGain = 12.6f;

        public override float CalculateGain(Redirector2 redirector, float deltaTime) {
            // Active state is dependent on if the component's `activeState` matches the current state
            active = (activeState & redirector.playerState) != 0;
            if (!active) return 0f;

            if (redirector.current_eye_rotation <= saccadeThreshold * Time.deltaTime) return 0f;
            return saccadeGain * deltaTime * redirector.direction_factor * redirector.speed_factor;
        }
    }
}
