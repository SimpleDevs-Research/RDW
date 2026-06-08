using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RDW {
    public class SelectionUI : MonoBehaviour
    {
        [System.Serializable]
        public class SelectionGroup {
            public string name;
            public Button selectionButton;
            public CanvasGroup selectedUI;
            
            public void ToggleUI(bool setTo = true) {
                selectedUI.interactable = setTo;
                selectedUI.alpha = (setTo) ? 1f : 0f;
            }
        }

        [Header("=== Selections ===")]
        public List<SelectionGroup> selections = new List<SelectionGroup>();
        private SelectionGroup selected = null;
        private Dictionary<string, SelectionGroup> selectionDict;

        private void Start() {
            selectionDict = new Dictionary<string, SelectionGroup>();
            foreach(SelectionGroup sg in selections) {
                if (!selectionDict.ContainsKey(sg.name)) {
                    selectionDict.Add(sg.name, sg);
                    sg.ToggleUI(false);
                }
            }
        }

        private void OnEnable() {
            if (selected != null) selected.ToggleUI(true);
        }

        public void Select(string name = null) {
            // If null, then we're just toggling everything off
            if (name == null && selected != null) {
                selected.ToggleUI(false);
                selected = null;
                return;
            }

            // Don't do anything if we don't have a record
            if (!selectionDict.ContainsKey(name)) return;

            // If already selected something, disable the current selected
            if (selected != null && selected.name != name) {
                selected.ToggleUI(false);
            }

            // Now set the new selected
            selected = selectionDict[name];
            selected.ToggleUI(true);
        }

        private void OnDisable() {
            if (selected != null) selected.ToggleUI(false);
        }
    }

}
