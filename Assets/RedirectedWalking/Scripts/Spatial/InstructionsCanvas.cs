using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class InstructionsCanvas : MonoBehaviour
{

    public enum DisplayType { None, Off, Constant, Fade_In_Out, Fade_Out, Fade_In}
    public enum RotationType { LookAt, Follow }

    [SerializeField]
    private Transform positionTarget = null;
    [SerializeField]
    private Transform rotationTarget = null;
    private CanvasGroup canvasGroup;
    
    [SerializeField]
    private float movementSpeed = 1f;
    [SerializeField]
    private AnimationCurve movementMultiplier;
    [SerializeField]
    private DisplayType opacityControl = DisplayType.Fade_In_Out;
    [SerializeField]
    private RotationType rotationType = RotationType.Follow;
    [SerializeField] private float distanceThreshold = 2f;
    [SerializeField] private float fadeTimeThreshold = 2f;
    [SerializeField] private float fadeTimeRate = 0.5f;

    private float startTime = 0f;
    private float distanceToTarget = 0f;
    private float gradientValue = 0f;
    private bool isClose = true;

    private void Awake() {
        startTime = Time.time;  // Time since the start of the application
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // Update is called once per frame
    void Update()
    {
        if (positionTarget != null) {
            distanceToTarget = Vector3.Distance(positionTarget.position, transform.position);
            gradientValue = Mathf.Clamp(distanceToTarget/distanceThreshold, 0f, 1f);
            isClose = distanceToTarget <  0.05f;
            UpdatePosition();
            UpdateOpacity();
        }
        if (rotationTarget != null) {
            UpdateRotation();
        }
    }

    private void UpdatePosition() {
        if (isClose) {
            transform.position = positionTarget.position;
            return;
        }
        float step = movementSpeed * Time.deltaTime * movementMultiplier.Evaluate(gradientValue);
        transform.position = Vector3.MoveTowards(transform.position, positionTarget.position, step);
    }

    private void UpdateRotation() {
        Quaternion targetRot;
        if (rotationType == RotationType.LookAt) {
            targetRot = Quaternion.LookRotation(transform.position - rotationTarget.position);
        }
        else {
            targetRot = rotationTarget.rotation;
        }
        transform.rotation = targetRot;
    }

    private void UpdateOpacity(float toSetAlpha=1f) {
        float newAlpha = toSetAlpha;
        switch(opacityControl) {
            case DisplayType.Fade_In_Out:
                newAlpha = (!isClose) ? 1f - gradientValue : 1f;
                canvasGroup.alpha = newAlpha;
                break;
            case DisplayType.Constant:
                newAlpha = 1f;
                canvasGroup.alpha = newAlpha;
                break;
            case DisplayType.Fade_Out:
                newAlpha = (Time.time - startTime < fadeTimeThreshold) 
                    ? 1f 
                    : 1f - Mathf.Clamp((Time.time - startTime+fadeTimeThreshold)/fadeTimeRate, 0f, 1f);
                canvasGroup.alpha = newAlpha;
                break;
            case DisplayType.Fade_In:
                newAlpha = (Time.time - startTime < fadeTimeThreshold) 
                    ? 0f 
                    : Mathf.Clamp((Time.time - startTime+fadeTimeThreshold)/fadeTimeRate, 0f, 1f);
                canvasGroup.alpha = newAlpha;
                break;
            case DisplayType.Off:
                newAlpha = 0f;
                canvasGroup.alpha = newAlpha;
                break;
        }
    }

    public void SetPositionTarget(Transform t = null) { positionTarget = t; }
    public void SetRotationTarget(Transform t = null) { rotationTarget = t; }
}
