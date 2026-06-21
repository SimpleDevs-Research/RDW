using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RVO;

namespace StreetSim {

    [System.Serializable]
    public class Route : ISerializationCallbackReceiver {

        [HideInInspector] public string name = "";
        
        // ==========================================
        // === Manual - You Must Set These! ===
        // ==========================================
        public RouteNode node1;
        public RouteNode node2;
        public bool is_active = true;
        public float pathWidth = 5;
        public float baseCost = 1;
        public float safety = 1;
        
        // ==========================================
        [Header("=== Computed - READ-ONLY ===")]
        // ==========================================
        [Tooltip("STATIC reference; is set during Route Manager's `Awake()` and never changes")]
        public PathRegion pathRegion;
        [Tooltip("STATIC property; is only calculated once and never changes")]
        public float distance;
        [Tooltip("DYNAMIC property; updated whenever its path region is also updated")]
        public float density;
        [Tooltip("Dynamic property; Updated whenever its path region is also updated")]
        public float dirtiness;

        public float computedCost;

        private void OnValidate() {
            if (node1 != null && node2 != null) {
                name = $"{node1.gameObject.name} - {node2.gameObject.name}";
                if (!is_active) name += " (INACTIVE)";
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize () => this.OnValidate();
        void ISerializationCallbackReceiver.OnAfterDeserialize () {}

    }
}
