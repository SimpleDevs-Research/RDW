using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RDW {
    
    [System.Serializable, RequireComponent(typeof(CanvasGroup))]
    public class CanvasGroupRef {
        public string name;
        public CanvasGroup canvas;
        public bool isActive => canvas.interactable;
        
        public void Toggle() {
            canvas.interactable = !canvas.interactable;
            canvas.blocksRaycasts = !canvas.blocksRaycasts;
            canvas.alpha = (canvas.interactable) ? 1f : 0f;
        }
        public void Toggle(bool interactable) {
            canvas.interactable = interactable;
            canvas.blocksRaycasts = interactable;
        }
        public void Toggle(float alpha) {
            canvas.alpha = alpha;
        }
        public void Toggle(bool interactable, float alpha) {
            canvas.alpha = alpha;
            canvas.interactable = interactable;
            canvas.blocksRaycasts = interactable;
        }
    }
    
    public class UIManager : MonoBehaviour
    {
        
        // This is a component that makes it easier to handle canvas groups, 
        // especially collections of groups. We can have multiples of these in the same scene.

        [Header("=== Canvas Groups ===")]
        public List<CanvasGroupRef> canvasGroups = new List<CanvasGroupRef>();
        private Dictionary<string, CanvasGroupRef> canvasGroupsDict;
        private CanvasGroupRef self;

        // At start, we create a dictionary to make it easier to search/query for specific canvas groups.
        private void Start() {
            self = new CanvasGroupRef {
                name = "self",
                canvas = GetComponent<CanvasGroup>()
            };
            canvasGroupsDict = new Dictionary<string, CanvasGroupRef>();
            foreach(var group in canvasGroups) {
                canvasGroupsDict.Add(group.name, group);
            }
            UpdateSelf();
        }

        // Every time we modify this script, we check if there are any duplicate entries 
        // that the user might have accidentally set.
        private void OnValidate() {
            if (canvasGroups.Count == 0) return;
            List<string> names = new List<string>();
            foreach(var group in canvasGroups) {
                if (!string.IsNullOrEmpty(group.name) && names.Contains(group.name)) Debug.LogError("Canvas Groups cannot share the same name.");
                else names.Add(group.name);
            }
        }

        // This can be called by any other component elsewhere.
        // This is used to toggle a specific group.
        // We also contain multiple override versions to account for different situations

        public void ToggleGroup(string query, bool interactable, float alpha) {
            if (TryGetCanvasGroup(query, out CanvasGroupRef group)) {
                group.Toggle(interactable, alpha);
                UpdateSelf();
            }
        }
        public void ToggleGroup(string query, bool interactable) {
            if (TryGetCanvasGroup(query, out CanvasGroupRef group)) {
                group.Toggle(interactable);
                UpdateSelf();
            }
        }
        public void ToggleGroup(string query, float alpha) {
            if (TryGetCanvasGroup(query, out CanvasGroupRef group)) {
                group.Toggle(alpha);
                UpdateSelf();
            }
        }
        public void ToggleGroup(string query) {
            if (TryGetCanvasGroup(query, out CanvasGroupRef group)) {
                group.Toggle();
                UpdateSelf();
            }
        }

        // If we want something more explicitly defined via function call, then we can use these functions
        public void ActivateGroup(string query) {
            ToggleGroup(query, true, 1f);
            UpdateSelf();
        }
        public void DeactivateGroup(string query) {
            ToggleGroup(query, false, 0f);
            UpdateSelf();
        }

        // These are all-group toggles. Due to this being a niche, we simply activate or deactivate
        public void ActivateAllGroups() {
            foreach(var group in canvasGroups) group.Toggle(true, 1f);
            self.Toggle(true, 1f);
        }
        public void DeactivateAllGroups() {
            foreach(var group in canvasGroups) group.Toggle(false, 0f);
            self.Toggle(false, 0f);
        }

        // Helper: Does a query string canvas name actually exist in memory?
        public bool TryGetCanvasGroup(string query, out CanvasGroupRef group) {
            group = null;
            if (!canvasGroupsDict.ContainsKey(query)) {
                Debug.LogError($"Try Toggle Group query \"{query}\" not found");
                return false;
            }
            group = canvasGroupsDict[query];
            return true;
        }

        private void UpdateSelf() {
            bool active = false;
            foreach(var group in canvasGroups) {
                active = active || group.isActive;
            }
            float alpha = active ? 1f : 0f;
            self.Toggle(active, alpha);
        }
    }
}
