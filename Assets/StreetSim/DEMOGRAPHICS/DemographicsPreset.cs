using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    
    // ======================================
    // === DEMOGRAPHIC PRESETS ===
    // A demographic "preset" is a collection of demographic groups you want to use in a session. For example,
    // - you can have one session dominated purely by one demographic group, or
    // - for a varied environment, you can have several demographic groups
    // For each group, you must define a proportion for it. This defines the weight or likelihood of 
    // spawning an agent in the respective demographic group during runtime.
    // ======================================

    [System.Serializable]
    public class DemographicSpawning {
        public Demographic demographic;
        [Range(0f,1f)] public float spawnWeight = 0f;
        public Color color = Color.black;
    }

    [CreateAssetMenu(fileName = "DemographicsPreset", menuName = "StreetSim/Demographics Preset", order = 1)]
    public class DemographicsPreset : ScriptableObject
    {

        public DemographicSpawning[] demographics;
        
        private void OnValidate() {
            float totalWeight = 0f;
            foreach(DemographicSpawning demographic in demographics) {
                totalWeight += demographic.spawnWeight;
            }
            if (totalWeight > 1f) {
                foreach(DemographicSpawning demographic in demographics) {
                    demographic.spawnWeight = demographic.spawnWeight / totalWeight;
                }
            }
        }
    }
}