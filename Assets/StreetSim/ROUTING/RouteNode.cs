using System.Collections.Generic;
using UnityEngine;
using RVO;

namespace StreetSim {
    public class RouteNode : MonoBehaviour
    {
        private static Vector3 horizontalEffect = new Vector3(1f,0f,1f);
        
        public float acceptableRadius = Mathf.Infinity;
        public Vector3 position;
        public Quaternion rotation;

        public Vector3 GetRandomPosition(Vector3 axisEffect) {
            return position + new Vector3(
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.x, 
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.y, 
                Random.Range(-acceptableRadius, acceptableRadius) * axisEffect.z
            );
        }
        public Vector3 GetRandomHorizontalPosition() {
            return GetRandomPosition(horizontalEffect);
        }

        public bool CheckWithinRadius(Vector3 localPosition, float buffer = 0f) {
            return (localPosition - position).magnitude <= acceptableRadius + buffer;
        }
 
        void OnDrawGizmosSelected() {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, acceptableRadius);
        }
    }
}