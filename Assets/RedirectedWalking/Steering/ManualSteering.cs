using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class ManualSteering : Steering
    {
        public bool isActive = false;
        public SteeringType defaultSteeringType = SteeringType.S2C;
        private Steering defaultSteering;

        public ManualSteering() {
            isActive = false;
            switch(defaultSteeringType) {
                case SteeringType.S2C:
                    defaultSteering = new S2C();
                    break;
                default:
                    defaultSteering = new Steering();
                    break;
            }
        }
        public override Direction GetDirection() {
            if (isActive) return currentDirection;
            return defaultSteering.GetDirection();
        }
        public override void SetDirection(Direction direction) {
            currentDirection = direction;
        }
        public override void SetDirection(float f) {
            currentDirection = (f>=0f) ? Direction.Right : Direction.Left;
        }
    }
}
