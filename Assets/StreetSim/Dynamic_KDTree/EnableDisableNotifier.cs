using UnityEngine;
using UnityEngine.Events;

namespace StreetSim {
    public class EnableDisableNotifier : MonoBehaviour
    {
        public UnityEvent<GameObject> onEnabled;    // Inspector listeners
        public UnityAction<GameObject> Enabled;     // Code listeners
        public UnityEvent<GameObject> onDisabled;   // Inspector listeners
        public UnityAction<GameObject> Disabled;    // Code listeners

        private void OnEnable() {
            onEnabled?.Invoke(this.gameObject);    // Inspector listener invoke
            Enabled?.Invoke(this.gameObject);      // Code listener invoke
        }
        private void OnDisable() {
            onDisabled?.Invoke(this.gameObject);   // Inspector listener invoke
            Disabled?.Invoke(this.gameObject);     // Code listener invoke
        }
    }
}
