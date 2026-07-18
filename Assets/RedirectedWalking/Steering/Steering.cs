using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    [System.Serializable]
    public class Steering
    {
        // ===============================
        // Enum for determining which direction to move in
        // ===============================
        public enum Direction { Left=-1, Right=1 }

        // ===============================
        // Enum for determining the Steering method
        // ===============================
        public enum SteeringType { Manual, S2C, SetDirection }

        public bool isActive = false;
        public Direction currentDirection = Direction.Right;

        public Steering() {
            isActive = true;
        }
        public virtual Direction GetDirection() {
            return currentDirection;
        }
        public virtual void SetDirection(Direction direction) {}
        public virtual void SetDirection(float f) {}
    }

}