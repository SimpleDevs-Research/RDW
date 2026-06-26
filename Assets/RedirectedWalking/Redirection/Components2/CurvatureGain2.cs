using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    [System.Serializable]
    public class CurvatureGain2 : GainComponent2
    {
        [Header("=== Rate-Based Curvature Gain ===")]
        [TextArea(4,1000)]
        public string description = "Curvature gain is the redirection gain obtained while the user is in motion. "
                                    + "This is one of the most common types of redirected walking, with various "
                                    + "parameters explored across different situations.";

        /// <summary>
        /// Steinicke et al. = 2.6 degrees/m
        ///     Frank Steinicke, Gerd Bruder, Jason Jerald, Harald Frenz, and Markus Lappe. 2010. 
        ///     Estimation of detection thresholds for redirected walking techniques.
        /// Grechkin et al. = 4.9 degrees/m
        ///     Timofey Grechkin, Jerald Thomas, Mahdi Azmandian, Mark Bolas, and Evan Suma. 2016. 
        ///     Revisiting detection thresholds for redirected walking: combining translation and curvature gains.
        /// Langbehn et al. = either 15.4 degrees/m or 31.7 degrees/m, depending on circumstance.
        ///     Eike Langbehn, Paul Lubos, Gerd Bruder, and Frank Steinicke. 2017b. 
        ///     Bending the Curve: Sensitivity to Bending of Curved Paths and Application in Room-Scale VR. 
        /// Reitzler et al. = 20 degrees/m
        ///     Michael Reitzler, Jan Gugenheimer, Teresa Hirzle, Martin Deubzer, Eike Langbehn, and Enrico Rukzio. 2018.
        ///     Rethinking Redirected Walking: On the Use of Curvature Gains Beyond Perceptual Limitations and Revisiting Bending Gains.
        /// </summary>
        
        [Space]
        private float[] preset_curvatures = new float[5] { 2.6f, 4.9f, 15.4f, 31.7f, 20f };
        public enum CurvatureRatePresets { Steinicke=0, Grechkin=1, Langbehn1=2, Langbehn2=3, Reitzler=4, Custom=-1 }
         
        [Tooltip("Curvature Preset. Choose `Custom` for a manual gain value. All other values are presets:\n- Steinicke = 2.6 degrees/m\n- Grechkin = 4.9 degrees/m\n- Langbehn = either 15.4 degrees/m or 31.7 degrees/m\n- Reitzler = 20 degrees/m")]
        public CurvatureRatePresets curvature_preset = CurvatureRatePresets.Custom;
        [Tooltip("The curvature gain (degrees per unit meter). This is replaced by a preset value if not CUSTOM set above.")]
        public float curvature_rate = 0f; 
        public bool weight_by_direction = true;

        public override void Enable() {
            base.Enable();
            if ((int)curvature_preset != -1) curvature_rate = preset_curvatures[(int)curvature_preset];
        }

        public override float CalculateGain(Redirector2 redirector, float deltaTime) {
            // Active state is dependent on if the component's `activeState` matches the current state
            active = (activeState & redirector.playerState) != 0;
            if (!active) {
                Debug.LogWarning("Curvature gain is inactive");
                _contribution = 0f;
                return _contribution;
            }
            // What's the dot product between the current move direction and the current forward?
            // Because if we're moving sideways, I think the illusion may break...
            float forward_weight = (weight_by_direction) 
                ? Mathf.Clamp(Vector3.Dot(Player.Instance.CurrentState.MoveDirection, Player.Instance.CurrentState.Forward), 0f, 1f)
                : 1f;
            //_contribution = curvature_rate * redirector.current_displacement.magnitude * forward_weight * redirector.direction_factor * redirector.speed_factor;
            _contribution = curvature_rate * Player.Instance.CurrentState.MoveDistance * forward_weight * redirector.direction_factor * redirector.speed_factor;
            return _contribution;
        }
    }
}
