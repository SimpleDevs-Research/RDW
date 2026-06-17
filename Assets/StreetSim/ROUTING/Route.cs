using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
#if UNITY_EDITOR
using UnityEditor;
#endif
*/

namespace StreetSim {

    [System.Serializable]
    public class Route {
        
        /*
        #if UNITY_EDITOR        
        [Help(
            "A \"Route\" is described as the straight path between two nodes on a graph with a constant `width`. Each "
            + "Route has some basic properties, such as a `baseCost` and `safety` metric. Other properties are computed "
            + "during runtime.", 
            UnityEditor.MessageType.None
        )]
        #endif
        */

        // ==========================================
        [Header("=== Manual - You Must Set These! ===")]
        // ==========================================
        public RouteNode node1;
        public RouteNode node2;
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
