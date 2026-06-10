using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    [System.Serializable]
    public class GainComponent2
    {
        [Tooltip("Similar to enabling/disabling MonoBehaviour components. This is set by YOU and doesn't get modified by any code.")]
        [SerializeField] private bool _enabled = true;
        [Tooltip("Even if a component is enabled, you can control whether this contributes to redirection during runtime.")]
        [SerializeField] private bool _active = true;
        [Tooltip("This is a layer mask of sorts; it lets you dictate when this component is active")]
        public Redirector2.PlayerState activeState;
        
        // If external code NEEDS to access these functions, then we can call their public versions. Except:
        // 1. For `enabled`, we enforce that the code doesn't modify this. So we simply set a getter
        public bool enabled => _enabled;
        // 2. For `active`, the code can technically change the public version of it. During `Enable()`, we set this to the value of `_active`.
        [HideInInspector] public bool active;
        // This is a fast check during runtime
        public bool activeDuringRuntime => enabled && active;

        public virtual void Enable() { active = _active; }
        public virtual void Disable() {}
        public virtual float CalculateGain(Redirector2 redirector, float deltaTime) { return 0f; }

        public virtual void Toggle() { active = !active; }
        public virtual void Toggle(bool new_active) { active = new_active; }
        public virtual void ToggleOn() { active = true; }
        public virtual void ToggleOff() { active = false; }
    }
}
