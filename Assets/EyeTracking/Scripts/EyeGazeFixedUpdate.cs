using System;
using UnityEngine;
using Meta.XR.Util;

public class EyeGazeFixedUpdate : MonoBehaviour
{
    public bool EyeTrackingEnabled => OVRPlugin.eyeTrackingEnabled;
    public EyeId Eye;
    public float Confidence { get; private set; }
    [Range(0f, 1f)] public float ConfidenceThreshold = 0.5f;
    public bool ApplyPosition = true;
    public bool ApplyRotation = true;
    private OVRPlugin.EyeGazesState _currentEyeGazesState;

    public Transform ReferenceFrame;
    [Tooltip(
        "HeadSpace: Tracking mode will convert the eye pose from tracking space to local space " +
        "which is relative to the VR camera rig. For example, we can use this setting to correctly " +
        "show the eye movement of a character which is facing in another direction than the source.\n" +
        "WorldSpace: Tracking mode will convert the eye pose from tracking space to world space.\n" +
        "TrackingSpace: Track eye is relative to OVRCameraRig. This is raw pose information from VR tracking space.")]
    public EyeTrackingMode TrackingMode;

    private Quaternion _initialRotationOffset;
    public Transform _viewTransform;

    private const OVRPermissionsRequester.Permission EyeTrackingPermission =
        OVRPermissionsRequester.Permission.EyeTracking;

    private Action<string> _onPermissionGranted;
    private static int _trackingInstanceCount;

    public bool recordData = true;
    public CSVWriter writer;
    private Vector3 prevOrientation;
    private Vector3 currentOrientation;
    private float rotationDisp;
    public Transform headRef;
    public Transform targetRef = null;
    public float saccadeAngleThreshold = 180f;
    public Color saccadeColor = Color.blue;
    public Color nonSaccadeColor = Color.yellow;
    private Material targetMaterial = null;

     private void Awake()
    {
        _onPermissionGranted = OnPermissionGranted;
        if (recordData) writer.Initialize();
    }

    private void Start()
    {
        PrepareHeadDirection();

        if (targetRef != null) {
            Renderer r = targetRef.GetComponent<Renderer>();
            if (r != null) targetMaterial = r.materials[0];
        }
        targetMaterial?.SetColor("_Color",nonSaccadeColor);
        prevOrientation = (_viewTransform != null) ? _viewTransform.InverseTransformDirection(transform.forward) : transform.forward;
        CacheCurrent();
        CachePrev();
    }

    private void OnEnable()
    {
        _trackingInstanceCount++;

        if (!StartEyeTracking())
        {
            enabled = false;
        }
    }

    private void OnPermissionGranted(string permissionId)
    {
        if (permissionId == OVRPermissionsRequester.GetPermissionId(EyeTrackingPermission))
        {
            OVRPermissionsRequester.PermissionGranted -= _onPermissionGranted;
            enabled = true;
        }
    }

    private bool StartEyeTracking()
    {
        if (!OVRPermissionsRequester.IsPermissionGranted(EyeTrackingPermission))
        {
            OVRPermissionsRequester.PermissionGranted -= _onPermissionGranted;
            OVRPermissionsRequester.PermissionGranted += _onPermissionGranted;
            return false;
        }

        if (!OVRPlugin.StartEyeTracking())
        {
            Debug.LogWarning($"[{nameof(OVREyeGaze)}] Failed to start eye tracking.");
            return false;
        }

        return true;
    }

    private void OnDisable()
    {
        if (--_trackingInstanceCount == 0)
        {
            OVRPlugin.StopEyeTracking();
        }
    }

    private void OnDestroy()
    {
        OVRPermissionsRequester.PermissionGranted -= _onPermissionGranted;
        if (recordData) writer.Disable();
    }

    private void Update()
    {
        if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref _currentEyeGazesState))
            return;

        var eyeGaze = _currentEyeGazesState.EyeGazes[(int)Eye];

        if (!eyeGaze.IsValid)
            return;

        var pose = eyeGaze.Pose.ToOVRPose();
        switch (TrackingMode)
        {
            case EyeTrackingMode.HeadSpace:
                pose = pose.ToHeadSpacePose();
                break;
            case EyeTrackingMode.WorldSpace:
                pose = pose.ToWorldSpacePose(Camera.main);
                break;
        }

        Vector3 pos = pose.position;
        Quaternion ori = pose.orientation;
        Confidence = eyeGaze.Confidence;

        // Finally, record everything in writing
        if (recordData) {
            writer.AddPayload(Time.frameCount);
            writer.AddPayload(Eye.ToString());
            writer.AddPayload(ori.eulerAngles);
            writer.AddPayload(Confidence);
            writer.WriteLine(true);
        }
        // Move back into default code
        if (Confidence < ConfidenceThreshold)
            return;
            
        if (ApplyPosition) {
            transform.position = pos;
        }
        if (ApplyRotation) {
            transform.rotation = CalculateEyeRotation(ori);
        }

        CacheCurrent();
        if (targetMaterial != null) {
            if (rotationDisp > saccadeAngleThreshold * Time.deltaTime) targetMaterial.SetColor("_Color",saccadeColor);
            else targetMaterial.SetColor("_Color",nonSaccadeColor);
        }
        CachePrev();
    }

    private Quaternion CalculateEyeRotation(Quaternion eyeRotation)
    {
        var eyeRotationWorldSpace = _viewTransform.rotation * eyeRotation;
        var lookDirection = eyeRotationWorldSpace * Vector3.forward;
        var targetRotation = Quaternion.LookRotation(lookDirection, _viewTransform.up);

        return targetRotation * _initialRotationOffset;
    }

    private void PrepareHeadDirection()
    {
        string transformName = "HeadLookAtDirection";

        _viewTransform = new GameObject(transformName).transform;

        if (ReferenceFrame)
        {
            _viewTransform.SetPositionAndRotation(ReferenceFrame.position, ReferenceFrame.rotation);
        }
        else
        {
            _viewTransform.SetPositionAndRotation(transform.position, Quaternion.identity);
        }

        _viewTransform.parent = transform.parent;
        _initialRotationOffset = Quaternion.Inverse(_viewTransform.rotation) * transform.rotation;
    }

    private void CacheCurrent() {
        currentOrientation = (headRef != null) ? headRef.InverseTransformDirection(transform.forward) : transform.forward;
        rotationDisp = Vector3.Angle(prevOrientation, currentOrientation);
    }
    private void CachePrev() {
        prevOrientation = currentOrientation;
    }

    /// <summary>
    /// List of eyes
    /// </summary>
    public enum EyeId
    {
        Left = OVRPlugin.Eye.Left,
        Right = OVRPlugin.Eye.Right
    }

    public enum EyeTrackingMode
    {
        HeadSpace,
        WorldSpace,
        TrackingSpace
    }
}
