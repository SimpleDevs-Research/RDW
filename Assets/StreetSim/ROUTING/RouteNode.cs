using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public class RouteNode : MonoBehaviour
    {
        private static Vector3 horizontalEffect = new Vector3(1f,0f,1f);

        public Vector3 position => transform.position;
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

        public bool CheckWithinRadius(Vector3 queryPosition, float buffer = 0f) {
            return (queryPosition - transform.position).magnitude <= acceptableRadius + buffer;
        }

        void OnDrawGizmosSelected() {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, acceptableRadius);
        }
    }
}