using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    [CreateAssetMenu(fileName = "GainSettings", menuName = "RDW/Gain Settings", order = 1)]
    public class GainSettings : ScriptableObject
    {
        [Tooltip("The name of the scene to be loaded. Must match a corresponding scene in `Additive Scene Manager`.")]
        public string sceneName;

        [Tooltip("Should we incorporate passthrough?")]
        public bool usePassthrough;
        
        [Header("=== Curvature Gain ===")]
        public CurvatureGain2 curvatureGain;

        [Header("=== Rotation Gain ===")]
        public RotationGain2 rotationGain;

        [Header("=== Saccade Gain ===")]
        public SaccadeGain2 saccadeGain;

        [Header("=== Manual Gain ===")]
        public ManualGain2 manualGain;

        [Header("=== Gain Settings by State ===")]
        public List<GainComponent2> defaultStateComponents;
    }
}