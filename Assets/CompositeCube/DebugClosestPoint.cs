using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugClosestPoint : MonoBehaviour
{
    public CapsulePrism prism;
    private Vector3 closestPoint;
    private Vector3 raycastOrigin;
    private float distance;

    private void Update() {
        closestPoint = prism.GetClosestPoint(transform.position, out Vector3 localPos, out distance);
    }

    private void OnDrawGizmos() {
        Gizmos.color = (distance > 0) ? Color.blue : Color.red;
        Gizmos.DrawLine(transform.position, closestPoint);
        Gizmos.DrawSphere(closestPoint, 0.15f);
    }


}
