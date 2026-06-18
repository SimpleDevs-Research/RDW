using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public class PathRegion : MonoBehaviour
    {
        
        [HideInInspector] public string Description = 
            "This component acts as the physical manifestation of a Route inside RouteManager. "
            + "It stores data for its connected Route, and Route will use that data during cost computation. "
            + "For your part, there's no need to modify ANY of these values. RouteManager needs to initialize "
            + "this during its `Awake()` function anyways.";

        [Header("=== COMPUTED PATH PROPERTIES ===")]
        public float density;       // Person per square meter unit
        public float dirtiness;     // Dirtiness level
        public float risk;          // Danger level
        public float size;          // In square meters

        public HashSet<Pedestrian> pedestrians = new();
        public HashSet<PathQualityEffector> effectors = new();


        // ==========================================
        // === INITIALIZE: Called by RouteManager ===
        // ==========================================
        public void Initialize() {
            size = transform.localScale.x * transform.localScale.z;
        }


        // ==========================================
        // === SOMETHING HAS ENTERED! ===
        // ==========================================
        public void OnTriggerEnter(Collider other) {
            
            // --------------------
            // DENSITY UPDATE
            // We only update density if successfully added to our hashet of pedestrians.
            // --------------------
            Pedestrian pc = other.GetComponent<Pedestrian>();
            if (pc != null && pedestrians.Add(pc)) {
                density = pedestrians.Count / size;
            }

            // --------------------
            // Path Quality Effector
            // We only update path quality if successfully added to our hashset of effectors.
            // --------------------
            PathQualityEffector pqe = other.GetComponent<PathQualityEffector>();
            if (pqe != null && effectors.Add(pqe)) {
                // Cleanliness effect
                if (pqe.myEffect == PathQualityEffector.effectType.cleanliness) {
                    dirtiness += pqe.effectLevel;
                }
                // Risk effect
                if (pqe.myEffect == PathQualityEffector.effectType.safety) {
                    risk += pqe.effectLevel;
                }
            }

        }

        // ==========================================
        // === SOMETHING HAS EXITED! ===
        // ==========================================
        public void OnTriggerExit(Collider other) {
            
            // --------------------
            // Density Update
            // We only update density if successfully removed from our hashet of pedestrians.
            // --------------------
            Pedestrian pc = other.GetComponent<Pedestrian>();
            if (pc != null && pedestrians.Remove(pc)) {
                density = pedestrians.Count / size;
            }

            // --------------------
            // Path Quality Effector
            // We only update path quality if successfully removed from our hashset of effectors.
            // --------------------
            PathQualityEffector pqe = other.GetComponent<PathQualityEffector>();
            if (pqe != null && effectors.Remove(pqe)) {
                // Cleanliness effect
                if (pqe.myEffect == PathQualityEffector.effectType.cleanliness) {
                    dirtiness -= pqe.effectLevel;
                }
                // Risk effect
                if (pqe.myEffect == PathQualityEffector.effectType.safety) {
                    risk -= pqe.effectLevel;
                }
            }

        }
    }
}