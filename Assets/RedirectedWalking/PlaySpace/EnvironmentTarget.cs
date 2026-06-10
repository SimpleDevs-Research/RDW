using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    [RequireComponent(typeof(Collider)), RequireComponent(typeof(Rigidbody))]
    public class EnvironmentTarget : MonoBehaviour
    {
        public UnityEvent onTargetReached;

        private void OnTriggerEnter() {
            onTargetReached?.Invoke();
        }
    }

}