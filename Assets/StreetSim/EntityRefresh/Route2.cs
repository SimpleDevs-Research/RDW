using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RVO;

namespace StreetSim {

    [System.Serializable]
    public class Route2 {
        
        // ==========================================
        [Header("=== Manual - You Must Set These! ===")]
        // ==========================================
        public RouteNode2 node1;
        public RouteNode2 node2;
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

    }
}
