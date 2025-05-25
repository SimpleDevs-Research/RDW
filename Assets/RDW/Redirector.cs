using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW
{
    public class Redirector : MonoBehaviour
    {
        [System.Serializable]
        public class DirectMap
        {
            public string name;
            public Transform tracked_obj;
            public Transform display_obj;
        }

        [Header("=== Head References ===")]
        [Tooltip("The true tracked head.\n- If using Meta SDK, it's the 'CenterEyeAnchor'.\n- For OpenXR, it's the Main Camera.\nMake sure to make this camera feed into a display OTHER than 'Display 1'.")]
        public Transform trackedHead;
        [Tooltip("The display head that the VR user sees.\nMake sure this one directly outputs to 'Display 1'.")]
        public Transform displayHead;

        [Header("=== Direct Mapped References ===")]
        [Tooltip("Direct Mapped References are items attached to the original tracking space of the VR user (i.e. hands)\nthat MUST follow the head's rotation and redirection.")]
        public List<DirectMap> mapped_refs;

        [Header("=== Settings ===")]
        [Tooltip("Toggle whether the redirection is actually applied per frame.")]
        public bool enable_redirection = true;
        private bool prev_enabled_redirection = true;
        [Tooltip("Call these whenever `enable_redirection` is toggled on or off")]
        public UnityAction redirection_enabled;
        public UnityAction redirection_disabled;
        public List<GainComponent> gain_components = new List<GainComponent>();

        private Vector3 cur_tracked_pos, prev_tracked_pos;
        private Vector3 cur_tracked_orientation, prev_tracked_orientation;
        private Vector3 cur_displacement, prev_displacement;
        private float cur_hor_rot;

        public float gain_angle = 30f;
        public float disabled_gain_angle = 0f;

        private Vector3 localChange;

        // Start is called before the first frame update
        void Start()
        {
            prev_tracked_pos = trackedHead.position.Flatten();
            prev_enabled_redirection = enable_redirection;
            CacheCurrent();
            CachePrev();
            foreach (GainComponent gc in gain_components) gc.Initialize(this);
        }

        // Update is called once per frame
        private void Update()
        {
            CacheCurrent();

            // Calculate gain angle
            // If `enable_redirection` is NOT toggled, then 
            //      we need to keep track of the disabled rotational gain 
            //      on TOP of the existing gain angle.
            float angle_disp = 0f;
            foreach (GainComponent gc in gain_components)
            {
                angle_disp += gc.CalculateGain();
            }
            gain_angle += angle_disp;

            CachePrev();

            // We need to update our cameras and other mapped references
            // We do this in the Update loop, not the LateUpdate, because OVR prefers to do all updates in the Update loop
            // If you try to do these in the LateUpdate, you'll see time lag in VR.
            UpdateCamera();
            if (mapped_refs.Count > 0)
            {
                foreach (DirectMap dm in mapped_refs) UpdateDirectMap(dm);
            }
        }

        public void UpdateCamera()
        {
            // First, let's update the rotation of the follower based on the redirection
            Quaternion gain_change = Quaternion.Euler(Vector3.up * gain_angle);
            Quaternion disabled_change = Quaternion.Euler(Vector3.up * disabled_gain_angle);
            displayHead.rotation = gain_change * disabled_change * trackedHead.rotation;
            // Second, we have to ensure that all translations in the real world are translated to the virtual cameram with respect to the angle gain
            localChange = trackedHead.InverseTransformDirection(cur_displacement);
            displayHead.position += displayHead.TransformDirection(localChange);
        }

        public void UpdateDirectMap(DirectMap dm)
        {
            // Get local position/rotation/scale of the original relative to the reference (tracked) head
            Vector3 localPos = trackedHead.InverseTransformPoint(dm.tracked_obj.position);
            Quaternion localRot = Quaternion.Inverse(trackedHead.rotation) * dm.tracked_obj.rotation;
            // Apply the same relative transform to the display object equivalent
            dm.display_obj.position = displayHead.TransformPoint(localPos);
            dm.display_obj.rotation = displayHead.rotation * localRot;
        }

        public void CachePrev()
        {
            prev_tracked_pos = cur_tracked_pos;
            prev_displacement = cur_displacement;
            prev_tracked_orientation = cur_tracked_orientation;
        }

        public void CacheCurrent()
        {
            cur_tracked_pos = trackedHead.position;
            cur_displacement = cur_tracked_pos - prev_tracked_pos;
            cur_tracked_orientation = Vector3.Normalize(trackedHead.forward.Flatten());
            cur_hor_rot = Vector3.SignedAngle(prev_tracked_orientation, cur_tracked_orientation, Vector3.up);
        }

        public void IncreaseAngleGain(float increment = 1f)
        {
            gain_angle += increment;
        }
        public void DecreaseAngleGain(float increment = 1f)
        {
            gain_angle -= increment;
        }

        public void Toggle() { enable_redirection = !enable_redirection; CheckEnabled(); }
        public void Toggle(bool e) { enable_redirection = e; CheckEnabled(); }
        public void ToggleOn() { enable_redirection = true; CheckEnabled(); }
        public void ToggleOff() { enable_redirection = false; CheckEnabled(); }

        // This is called only with inspector changes.
        private void OnValidate()
        {
            // Don't do anything if the application is nto active
            if (!Application.isPlaying) return;
            // Check if enable_redirection is toggled or not
            CheckEnabled();
        }

        private void CheckEnabled() {
            if (enable_redirection != prev_enabled_redirection)
            {
                if (enable_redirection) redirection_enabled?.Invoke();
                else redirection_disabled?.Invoke();
            }
            prev_enabled_redirection = enable_redirection;
        }

        private void OnDestroy()
        {
            foreach (GainComponent gc in gain_components) gc.OnDestroy();
        }
    }
}
