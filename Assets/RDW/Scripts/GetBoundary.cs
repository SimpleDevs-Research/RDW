using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public class GetBoundary : MonoBehaviour
{
    private List<GameObject> boundary_objects = new List<GameObject>();
    private void Start() {
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        if (loader == null) {
            Debug.LogWarning("Could not get active Loader.");
            return;
        }

        var inputSubsystem = loader.GetLoadedSubsystem<XRInputSubsystem>();
        inputSubsystem.boundaryChanged += InputSubsystem_boundaryChanged;
    }

    private void InputSubsystem_boundaryChanged(XRInputSubsystem inputSubsystem) {
        List<Vector3> boundaryPoints = new List<Vector3>();
        boundary_objects = new List<GameObject>();
        if (inputSubsystem.TryGetBoundaryPoints(boundaryPoints)) {
            foreach (var point in boundaryPoints) {
                Debug.Log(point);
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere );
                sphere.transform.localScale = Vector3.one * 0.02f;
                sphere.GetComponent<Renderer>().material.color = Color.cyan;
                sphere.transform.position = point;
                boundary_objects.Add(sphere);
            }
        }
        else {
            Debug.LogWarning($"Could not get Boundary Points for Loader");
        }
    }
}
