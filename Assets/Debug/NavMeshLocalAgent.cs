using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshLocalAgent : MonoBehaviour
{
    public Transform parent;
    public float speed = 1f;
    public float destination_buffer = 0.1f;

    // The difficulty is local to world point. Since the virtual world is rotating, it becomes a bit weird.
    // Use `SetDestination(Vector3)` to declare where the destination point is. Note that by default it expects a world position, though you can toggle whether it converts from world to local via the extra boolean toggle
    // In any universe though, `destination` is expected to be in LOCAL position.
    // Then, when `CalculatePath()` is called, you must TRICK it into thinking that you're still in a static navmesh. So you need to use local positions for everything.
    // The outputted points should be in local space, by technicality. So no need to convert them into local - they should be already.
    private Vector3 destination;
    public List<Vector3> points = new List<Vector3>();

    private void Update() {
        // Path is not set
        if (points.Count <= 1) return;
        // Check if we're close to destination
        if (Vector3.Distance(this.transform.localPosition, this.destination) < destination_buffer) return;
        // Move along path
        Vector3 toCur = points[1] - this.transform.localPosition;
        Vector3 FromPrevToCur = points[1] - points[0];
        float dot = Vector3.Dot(toCur, FromPrevToCur);
        if (Vector3.Distance(this.transform.localPosition, points[1]) < destination_buffer || dot <= 0f) {
            // Surpassed. Move down the list
            points.RemoveAt(0);
        }
        if (points.Count > 1) transform.localPosition += speed * (points[1]-this.transform.localPosition).normalized * Time.deltaTime;

    }
    public void SetDestination(Vector3 d, bool convertToLocal = true) {
        this.destination = (convertToLocal) ? parent.InverseTransformPoint(d) : d;
        CalculatePath();
    }
    private void CalculatePath() {
        NavMeshPath nmp = new NavMeshPath();
        if (NavMesh.CalculatePath(
            this.transform.localPosition, 
            this.destination, 
            NavMesh.AllAreas, 
            nmp)
        ) {
            this.points = new List<Vector3>(nmp.corners);
        } else {
            Debug.LogError($"Cannot calculate path to {this.destination}");
        }
    }
}
