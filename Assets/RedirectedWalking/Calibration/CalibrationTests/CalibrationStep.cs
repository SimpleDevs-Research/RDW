using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    [System.Serializable]
    public class CalibrationStep : MonoBehaviour
    {
        // Protected because we want our inheritors to access it.
        protected bool _calibrated = false;
        public bool calibrated {
            get { return _calibrated; }
            set {}
        }

        // We run OnEnable() and OnDisable() to add events to OnCalibrationStart and OnCalibrationEnd
        protected virtual void OnEnable() {
            OnCalibrationStart.AddListener(ActivateUI);
            OnCalibrationEnd.AddListener(DeactivateUI);
        }
        protected virtual void OnDisable() {
            OnCalibrationStart.RemoveListener(ActivateUI);
            OnCalibrationEnd.RemoveListener(DeactivateUI);
        }

        // This can be called by any parent MonoBehaviour. We separate this from `Calibrate`
        // because we want the `OnCalibrationStart` and `OnCalibrationEnd` operations to
        // run all the time. So we let people modify `Calibrate` but not `Initialize`.
        public virtual IEnumerator Initialize() { 
            OnCalibrationStart?.Invoke();
            yield return StartCoroutine(Calibrate());
            OnCalibrationEnd?.Invoke();
        }

        // The calibration operation and update loop. 
        // Is a coroutine, so must be instantiated via `StartCoroutine()`.
        public virtual IEnumerator Calibrate() { yield return null; }

        // The toggling of calibration flag
        public virtual void SetCalibrated(bool setTo) { _calibrated = setTo; }

        // The on-offing of UI elements
        protected virtual void ActivateUI() {
            if (UIElements.Length > 0) foreach(GameObject go in UIElements) go.SetActive(true);
        }
        protected virtual void DeactivateUI() {
            if (UIElements.Length > 0) foreach(GameObject go in UIElements) go.SetActive(false);
        }

        // Unity events: when calibration is either started or finished, invoke an event if needed
        [Header("=== Core Elements ===")]
        public UnityEvent OnCalibrationStart;
        public UnityEvent OnCalibrationEnd;
        public GameObject[] UIElements;
    }
}