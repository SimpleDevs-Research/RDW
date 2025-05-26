using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW
{
    public class GainComponent : MonoBehaviour
    {
        public Redirector redirector;
        public bool active = true;

        // Called only once in its lifetime
        public virtual void Initialize(Redirector r) { SetRedirector(r); }
        // Called only once in its lifetime
        public virtual void OnDestroy() { SetRedirector(null);  }

        public virtual void SetRedirector(Redirector r) { redirector = r; }
        public virtual float CalculateGain() { return 0f; }

        public virtual void Toggle(bool new_active) { active = new_active; }
        public virtual void ToggleOn() { active = true; }
        public virtual void ToggleOff() { active = false; }
    }   
}
