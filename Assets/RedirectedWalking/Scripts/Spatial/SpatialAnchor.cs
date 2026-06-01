using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpatialAnchor : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public float lineHeight = 5f;

    private void Awake() {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
    }

    private void Start() {
        UpdatePosition();
    }

    public void UpdatePosition() {
        if (lineRenderer == null) return;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, transform.position + new Vector3(0f, lineHeight, 0f));
    }
}
