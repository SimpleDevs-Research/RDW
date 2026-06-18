using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public class RouteNode : Entity
    {
        private static Vector3 horizontalEffect = new Vector3(1f,0f,1f);
        public float acceptableRadius = Mathf.Infinity;

        public Vector3 GetRandomPosition(Vector3 axisEffect) {
            return transform.position + new Vector3(
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.x, 
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.y, 
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.z
            );
        }
        public Vector3 GetRandomHorizontalPosition() {
            return GetRandomPosition(horizontalEffect);
        }
        public Vector3 GetRandomLocalPosition(Vector3 axisEffect) {
            return transform.localPosition + new Vector3(
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.x, 
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.y, 
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.z
            );
        }
        public Vector3 GetRandomHorizontalLocalPosition() {
            return GetRandomLocalPosition(horizontalEffect);
        }

        public bool CheckWithinRadius(Vector3 worldPosition, float buffer = 0f) {
            return (worldPosition - transform.position).magnitude <= acceptableRadius + buffer;
        }
        public bool CheckWithinLocalRadius(Vector3 localPosition, float buffer = 0f) {
            return (localPosition - transform.localPosition).magnitude <= acceptableRadius + buffer;
        }
 
        void OnDrawGizmosSelected() {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, acceptableRadius);
        }
    }
}