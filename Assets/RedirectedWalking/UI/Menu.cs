using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RDW {
    
    [System.Serializable]
    public class ButtonInput {
        public string name;
        public OVRInput.Button input;
        public bool enabled;
        public UnityEvent events;
    }

    public class Menu : MonoBehaviour 
    {
        public List<ButtonInput> buttonInteractions;
        private Dictionary<string, ButtonInput> buttonInteractionsDictionary;

        private void Start() {
            buttonInteractionsDictionary = new Dictionary<string, ButtonInput>();
            foreach(ButtonInput bi in buttonInteractions) {
                if (!buttonInteractionsDictionary.ContainsKey(bi.name)) {
                    buttonInteractionsDictionary.Add(bi.name, bi);
                }
            }
        }

        private void Update() {
            foreach(ButtonInput bi in buttonInteractions) 
                if (bi.enabled && OVRInput.GetDown(bi.input)) 
                    bi.events?.Invoke();
        }

        public void TryInvokeButtonInteraction(string query) {
            if (!buttonInteractionsDictionary.ContainsKey(query)) {
                Debug.LogError("Cannot invoke event; query interaction name isn't found");
                return;
            }
            buttonInteractionsDictionary[query].events?.Invoke();
        }
    }
}
