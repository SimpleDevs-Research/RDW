using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NavMeshScreenDest : MonoBehaviour
{
    public NavMeshLocalAgent myAgent;
    private Vector3 last_hit;

    void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.TransformPoint(last_hit), 0.25f);
    }

    void Update() {
        if (Input.GetMouseButtonDown(0) && myAgent != null) {
            SetDestinationToMousePosition();
        }
    }

    void SetDestinationToMousePosition() {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit)) {
            last_hit = transform.InverseTransformPoint(hit.point);
            myAgent.SetDestination(hit.point, false);
        }
    }
}
