using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW{
    [ExecuteInEditMode]
    public class DebugRayRectIntersect : MonoBehaviour
    {
        public Transform parent;
        public Transform rect_min_ref, rect_max_ref;
        public Transform playerRef;

        private Vector3 intersection;

        void OnDrawGizmos() {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(intersection, 0.25f);
        }

        // Update is called once per frame
        private void Update() {
            Vector3 direction = parent.InverseTransformDirection(Vector3.Normalize(playerRef.forward.Flatten()));
            Vector3 origin = parent.InverseTransformPoint(playerRef.position.Flatten());
            Vector3 rectMin = parent.InverseTransformPoint(rect_min_ref.position.Flatten());
            Vector3 rectMax = parent.InverseTransformPoint(rect_max_ref.position.Flatten());

            Vector3 invDir = new Vector3(1f/direction.x, 0f, 1f/direction.z);
            float t1 = (rectMin.x - origin.x) * invDir.x;
            float t2 = (rectMax.x - origin.x) * invDir.x;
            float t3 = (rectMin.z - origin.z) * invDir.z;
            float t4 = (rectMax.z - origin.z) * invDir.z;

            float tMin = Mathf.Max(Mathf.Min(t1, t2), Mathf.Min(t3, t4)); // Entry (we ignore this)
            float tMax = Mathf.Min(Mathf.Max(t1, t2), Mathf.Max(t3, t4)); // Exit

            intersection = parent.TransformPoint(origin + direction * tMax);
        }

    }
}
