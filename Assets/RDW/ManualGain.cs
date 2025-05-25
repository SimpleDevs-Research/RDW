using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW
{
    public class ManualGain : GainComponent
    {
        public Vector3 ref_forward;

        public override void Initialize(Redirector r)
        {
            SetRedirector(r);
            redirector.redirection_enabled += this.ToggleOff;
            redirector.redirection_disabled += this.ToggleOn;
        }

        public override void OnDestroy()
        {
            redirector.redirection_enabled -= this.ToggleOff;
            redirector.redirection_disabled -= this.ToggleOn;
        }

        public override float CalculateGain()
        {
            if (redirector == null || !active) return 0f;
            // The gain angle for this component is 
            //      actually NEGATIVE of the displacement between the ref_forward and the curernt forward
            //      This is because when you're manually turning, you're expecting the display cam
            //      to stay in place while your tracked head rotates. So the display cam has to
            //      rotate to counter the manual rotation of the user
            Vector3 cur_forward = redirector.trackedHead.forward.Flatten();
            float gain_angle = -Vector3.SignedAngle(ref_forward, cur_forward, Vector3.up);
            // Gain components are supposed to return only the change in that frame. So we have to re-set our ref_forward
            ref_forward = cur_forward;
            // return the gain angle of this frame
            return gain_angle;
        }

        public override void ToggleOn()
        {
            Debug.Log("Manual Gain Toggle On");
            base.ToggleOn();
            ref_forward = redirector.trackedHead.forward.Flatten();
        }
    }
}
