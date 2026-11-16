using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using System.IO;

/// <summary>
/// Read-only diagnostics for XR camera projection and pose.
/// Attach to the actual center-eye Camera (or assign targetCamera).
/// </summary>
public class XRProjectionDiagnostics : MonoBehaviour
{
    public Camera targetCamera;
    [Min(0f)] public float delaySeconds = 30f;
    [Tooltip("Transform whose local coordinate system matches the logged pedestrian positions.")]
    public Transform referenceTransform;
    public bool logOnStart = true;
    public bool logOnKeyPress = false;
    public KeyCode logKey = KeyCode.F9;

    private IEnumerator Start()
    {
        if (!logOnStart) yield break;
        yield return new WaitForSecondsRealtime(delaySeconds);
        yield return new WaitForEndOfFrame();
        PrintReport();
    }

    private void Update()
    {
        if (logOnKeyPress && Input.GetKeyDown(logKey)) PrintReport();
    }

    [ContextMenu("Print XR Projection Report")]
    public void PrintReport()
    {
        Camera cam = targetCamera != null ? targetCamera : GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[XRProjection] No camera found. Assign the center-eye camera.");
            return;
        }

        var sb = new StringBuilder(4096);
        sb.AppendLine("========== XR PROJECTION DIAGNOSTICS ==========");
        sb.AppendLine($"Device: {SystemInfo.deviceModel} | Unity: {Application.unityVersion}");
        sb.AppendLine($"XR enabled: {XRSettings.enabled} | XR active: {XRSettings.isDeviceActive} | XR device: {XRSettings.loadedDeviceName}");
        sb.AppendLine($"Camera: {cam.name} | stereoEnabled: {cam.stereoEnabled} | stereoTargetEye: {cam.stereoTargetEye}");
        sb.AppendLine($"Camera world position: {Vec(cam.transform.position)}");
        sb.AppendLine($"Camera world rotation (Euler degrees): {Vec(cam.transform.eulerAngles)}");
        Quaternion q = cam.transform.rotation;
        sb.AppendLine($"Camera world rotation (quaternion xyzw): ({q.x:F6}, {q.y:F6}, {q.z:F6}, {q.w:F6})");
        if (referenceTransform != null)
        {
            Vector3 localPosition = referenceTransform.InverseTransformPoint(cam.transform.position);
            Quaternion localRotation = Quaternion.Inverse(referenceTransform.rotation) * cam.transform.rotation;
            sb.AppendLine($"Reference transform: {referenceTransform.name}");
            sb.AppendLine($"Camera reference-local position: {Vec(localPosition)}");
            sb.AppendLine($"Camera reference-local rotation (Euler degrees): {Vec(localRotation.eulerAngles)}");
            sb.AppendLine($"Camera reference-local rotation (quaternion xyzw): ({localRotation.x:F6}, {localRotation.y:F6}, {localRotation.z:F6}, {localRotation.w:F6})");
        }
        else
        {
            sb.AppendLine("Reference transform: NOT ASSIGNED (only world-space camera pose available)");
        }
        sb.AppendLine($"Camera.fieldOfView (vertical): {cam.fieldOfView:F4} deg | Camera.aspect: {cam.aspect:F6}");
        sb.AppendLine($"Camera pixel size: {cam.pixelWidth} x {cam.pixelHeight} | near/far: {cam.nearClipPlane:F4}/{cam.farClipPlane:F4} m");

        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances(displays);
        sb.AppendLine($"XR display subsystem count: {displays.Count}");
        foreach (var display in displays)
            sb.AppendLine($"XR display: {display.SubsystemDescriptor.id} | running: {display.running}");

        if (XRSettings.isDeviceActive)
            sb.AppendLine($"XR eye texture: {XRSettings.eyeTextureWidth} x {XRSettings.eyeTextureHeight} (texture dimensions, not optical FOV)");

        if (cam.stereoEnabled && XRSettings.isDeviceActive)
        {
            ReportProjection(sb, "LEFT EYE", cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));
            ReportProjection(sb, "RIGHT EYE", cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right));
        }
        else
        {
            sb.AppendLine("Stereo XR camera not active; reporting MONO camera projection only.");
            ReportProjection(sb, "MONO", cam.projectionMatrix);
        }
        sb.AppendLine("NOTE: Angles describe the projection frustum, not optical lens FOV or actual visible pixels after masks/occlusion.");
        sb.AppendLine("========== END XR PROJECTION DIAGNOSTICS ==========");
        // Save the complete diagnostic report to persistent storage.
        string filePath = Path.Combine( Application.persistentDataPath, "XRProjectionDiagnostics.txt");
        try {
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"[XRProjection] Report saved to: {filePath}");
        }
        catch (System.Exception ex) {
            Debug.LogError($"[XRProjection] Failed to save report: {ex}");
        }
    }

    private static void ReportProjection(StringBuilder sb, string label, Matrix4x4 m)
    {
        sb.AppendLine($"--- {label} ---");
        // Unity's conventional perspective projection has m32 = -1 and m33 = 0.
        // A matrix may temporarily be identity before XR initializes.
        if (Mathf.Abs(m.m00) < 1e-6f || Mathf.Abs(m.m11) < 1e-6f ||
            Mathf.Abs(m.m32 + 1f) > 0.01f || Mathf.Abs(m.m33) > 0.01f)
        {
            sb.AppendLine("Projection not a valid conventional perspective matrix (XR may not be initialized).");
            sb.AppendLine(m.ToString("F6"));
            return;
        }

        // For x_clip = m00*x + m02*z, w_clip = -z, with forward = -Z:
        // tan(left) = (m02-1)/m00; tan(right) = (m02+1)/m00.
        float left = Mathf.Atan((m.m02 - 1f) / m.m00) * Mathf.Rad2Deg;
        float right = Mathf.Atan((m.m02 + 1f) / m.m00) * Mathf.Rad2Deg;
        float bottom = Mathf.Atan((m.m12 - 1f) / m.m11) * Mathf.Rad2Deg;
        float top = Mathf.Atan((m.m12 + 1f) / m.m11) * Mathf.Rad2Deg;
        float hfov = right - left;
        float vfov = top - bottom;
        float aspect = ((m.m02 + 1f) / m.m00 - (m.m02 - 1f) / m.m00) /
                       ((m.m12 + 1f) / m.m11 - (m.m12 - 1f) / m.m11);

        sb.AppendLine($"Horizontal FOV: {hfov:F4} deg | Vertical FOV: {vfov:F4} deg");
        sb.AppendLine($"Horizontal extents: left {left:F4} deg, right {right:F4} deg");
        sb.AppendLine($"Vertical extents: bottom {bottom:F4} deg, top {top:F4} deg");
        sb.AppendLine($"Projection-plane aspect (width/height): {aspect:F6}");
        sb.AppendLine($"Projection m00/m02/m11/m12: {m.m00:F8}, {m.m02:F8}, {m.m11:F8}, {m.m12:F8}");
        sb.AppendLine("Projection matrix:\n" + m.ToString("F6"));
    }

    private static string Vec(Vector3 v) => $"({v.x:F6}, {v.y:F6}, {v.z:F6})";
}
