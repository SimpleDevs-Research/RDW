using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class S2C : Steering
    {

        public S2C() {
            isActive = true;
        }

        public override Direction GetDirection() {
            currentDirection = DeriveDirection();
            return currentDirection;
        }

        public Direction DeriveDirection() {
            float dir_dot = Vector3.Dot(
                RDW.Instance.worldCenter - RDW.Instance.headPoseAnchor.position.Flatten(), 
                RDW.Instance.headPoseAnchor.right.Flatten()
            );
            return (dir_dot < 0f) ? Direction.Left : Direction.Right;
        }
    }
}
