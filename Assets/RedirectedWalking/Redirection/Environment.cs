using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class Environment : MonoBehaviour
    {
        public Vector3 worldCenterOffset = Vector3.zero;

        private void OnDrawGizmosSelected() {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(Vector3.zero, worldCenterOffset);
            Gizmos.DrawSphere(worldCenterOffset, 0.05f);
        }

        // If enabled, then we must tell `redirector` that this is our current environment
        private void OnEnable() {
            if (Redirector2.Instance != null) {
                Redirector2.Instance.environmentParent = this.transform;
            }
            if (RDW.Instance != null) {
                transform.position = RDW.Instance.worldCenter - worldCenterOffset;
            }
        }

        // If disabled (e.g. when additive scene is unloaded), try to unset this transform as the environment parent in Redirector
        private void OnDisable() {
            if (Redirector2.Instance != null && Redirector2.Instance.environmentParent == this.transform) {
                Redirector2.Instance.environmentParent = null;
            }
        }

        // This is a special function event handler that can be called if the scene needs to be unloaded from within the scene.
        public void EnvironmentComplete() {
            RDW.Instance.UnloadEnvironment();
        }
    }
}
