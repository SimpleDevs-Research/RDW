using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class PivotMinimap : MonoBehaviour
    {
        public RectTransform headPoseAnchorSprite;
        public RectTransform rawPivotSprite;
        public RectTransform smoothPivotSprite;

        public void UpdateMinimap(
            Vector3 headPose,
            Vector3 rawPivot,
            Vector3 smoothPivot
        ) {

        }
    }
}
