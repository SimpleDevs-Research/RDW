using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace StreetSim {
    public class RVONavigation : INavigation
    {

        [System.Serializable]
        public struct DirData {
            public int index;
            public float2 direction;
            public float base_penalty;
            public float time_cost;
            public float penalty => base_penalty + time_cost;
            public DirData(int index, Vector2 direction, float base_penalty=0f, float time_cost=0f) {
                this.index = index;
                this.direction = (float2)direction;
                this.base_penalty = base_penalty;
                this.time_cost = time_cost;
            }
            public void UpdateTimeCost(float newCost) {
                if (newCost > this.time_cost) this.time_cost = newCost;
            }
        }

        [System.Serializable]
        public struct DirPenalty {
            public int index;
            public float penalty;
            public DirPenalty(int index, float penalty=0f) {
                this.index = index;
                this.penalty = penalty;
            }
        }

        private Vector3 _optimalVelocity;
        private Vector3 _localDestination;

        public Vector3 optimalVelocity => _optimalVelocity;
        public Vector3 localDestination => _localDestination;

        public void Tick() {

        }
    }

}